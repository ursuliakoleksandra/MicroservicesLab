using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Platform.Caching;

public sealed class TwoLevelCacheOptions
{
    /// <summary>Скільки чекати відповіді Redis, перш ніж вважати його недоступним для цього виклику.</summary>
    public TimeSpan L2Timeout { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>Після збою Redis L2 пропускається на цей час (простий circuit breaker): сервіс не чекає таймаут щоразу.</summary>
    public TimeSpan L2RetryAfterFailure { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>TTL ключа версії групи в Redis.</summary>
    public TimeSpan VersionTtl { get; set; } = TimeSpan.FromDays(7);
}

/// <summary>
/// Ручна реалізація дворівневого cache-aside (Лаб. 06, ч. 3.3): L1 = IMemoryCache із SizeLimit і політиками витіснення,
/// L2 = IDistributedCache (Redis), серіалізація System.Text.Json. Якщо Redis недоступний, кеш деградує до L1 + база даних.
/// </summary>
public sealed class TwoLevelCache : ITwoLevelCache
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IMemoryCache _memory;
    private readonly IDistributedCache _distributed;
    private readonly CacheMetrics _metrics;
    private readonly ILogger<TwoLevelCache> _logger;
    private readonly TwoLevelCacheOptions _options;
    private readonly TimeProvider _time;

    private readonly ConcurrentDictionary<string, string> _localVersions = new();
    private long _l1Hits, _l1Misses, _l2Hits, _l2Misses, _invalidations, _errors;
    private long _l2RetryAtTicks;   // 0 — L2 доступний; інакше момент (UTC ticks), після якого можна пробувати знову

    public TwoLevelCache(IMemoryCache memory, IDistributedCache distributed, CacheMetrics metrics,
        ILogger<TwoLevelCache> logger, TwoLevelCacheOptions? options = null, TimeProvider? time = null)
    {
        _memory = memory;
        _distributed = distributed;
        _metrics = metrics;
        _logger = logger;
        _options = options ?? new TwoLevelCacheOptions();
        _time = time ?? TimeProvider.System;

        // розмір і заповненість L1 — gauge-метрики (потрібен TrackStatistics = true в AddMemoryCache)
        if (memory is MemoryCache mc)
        {
            metrics.Meter.CreateObservableGauge("cache.l1.entries", () => mc.GetCurrentStatistics()?.CurrentEntryCount ?? 0,
                description: "Кількість записів у кеші пам'яті");
            metrics.Meter.CreateObservableGauge("cache.l1.size", () => mc.GetCurrentStatistics()?.CurrentEstimatedSize ?? 0,
                description: "Зайнятий SizeLimit (в одиницях Size)");
        }
    }

    // ------------------------------------------------------------------ читання

    public async Task<T?> GetAsync<T>(string key, CachePolicy policy, CancellationToken ct) where T : class
    {
        var sw = Stopwatch.StartNew();

        if (policy.EffectiveL1Absolute is not null)
        {
            if (_memory.TryGetValue(key, out L1Entry? entry) && entry!.Value is T cached)
            {
                Interlocked.Increment(ref _l1Hits);
                _metrics.Hit("L1");
                _metrics.Duration("L1", "get", sw.Elapsed.TotalMilliseconds);
                _logger.LogDebug("Cache hit {Key} level {Level}, ttl left {TtlLeftMs} ms, size {SizeBytes} bytes",
                    key, "L1", (long)Math.Max(0, (entry.ExpiresAt - _time.GetUtcNow()).TotalMilliseconds), entry.Bytes);
                return cached;
            }

            Interlocked.Increment(ref _l1Misses);
            _metrics.Miss("L1");
            _logger.LogDebug("Cache miss {Key} level {Level}", key, "L1");
        }

        var (l2Queried, l2Bytes) = await TryReadL2Async(key, ct);
        if (l2Bytes is { } bytes)
        {
            var value = Deserialize<T>(key, bytes);
            if (value is not null)
            {
                Interlocked.Increment(ref _l2Hits);
                _metrics.Hit("L2");
                _logger.LogDebug("Cache hit {Key} level {Level}, ttl {Ttl} s, size {SizeBytes} bytes",
                    key, "L2", policy.L2Absolute.TotalSeconds, bytes.Length);
                SetL1(key, value, policy, bytes.Length);                 // L1 заповнюється на зворотному шляху
                return value;
            }
        }

        if (l2Queried)
        {
            Interlocked.Increment(ref _l2Misses);
            _metrics.Miss("L2");
            _logger.LogDebug("Cache miss {Key} level {Level}", key, "L2");
        }
        return null;
    }

    public async Task<T?> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T?>> factory, CachePolicy policy,
        CancellationToken ct) where T : class
    {
        var cached = await GetAsync<T>(key, policy, ct);
        if (cached is not null) return cached;

        _logger.LogDebug("Cache fallback to source {Key}", key);
        var value = await factory(ct);                                    // база даних
        if (value is null) return null;                                   // відсутність не кешуємо

        await SetAsync(key, value, policy, ct);                           // L2 і L1 — на зворотному шляху
        return value;
    }

    // ------------------------------------------------------------------ запис

    public async Task SetAsync<T>(string key, T value, CachePolicy policy, CancellationToken ct) where T : class
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, Json);

        if (IsL2Available())
        {
            var sw = Stopwatch.StartNew();
            try
            {
                await _distributed.SetAsync(key, bytes,
                        new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = policy.L2Absolute }, ct)
                    .WaitAsync(_options.L2Timeout, ct);
                _metrics.Duration("L2", "set", sw.Elapsed.TotalMilliseconds);
                _logger.LogDebug("Cache set {Key} level {Level}, ttl {Ttl} s, size {SizeBytes} bytes",
                    key, "L2", policy.L2Absolute.TotalSeconds, bytes.Length);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                MarkL2Failed(ex, "set", key);
            }
        }

        SetL1(key, value, policy, bytes.Length);
    }

    // ------------------------------------------------------------------ інвалідація

    public Task RemoveAsync(string key, string reason, CancellationToken ct) => RemoveAsync([key], reason, ct);

    public async Task RemoveAsync(IEnumerable<string> keys, string reason, CancellationToken ct)
    {
        var list = keys.ToList();
        foreach (var key in list)
        {
            _memory.Remove(key);
            if (!IsL2Available()) continue;
            try
            {
                await _distributed.RemoveAsync(key, ct).WaitAsync(_options.L2Timeout, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                MarkL2Failed(ex, "remove", key);
            }
        }

        Interlocked.Add(ref _invalidations, list.Count);
        _metrics.Invalidated(reason);
        _logger.LogInformation("Cache invalidated {Keys}, reason {Reason}", list, reason);
    }

    public async Task<string> GetVersionAsync(string scope, CancellationToken ct)
    {
        var key = VersionKey(scope);
        if (IsL2Available())
        {
            try
            {
                var bytes = await _distributed.GetAsync(key, ct).WaitAsync(_options.L2Timeout, ct);
                if (bytes is not null)
                {
                    var version = Encoding.UTF8.GetString(bytes);
                    _localVersions[scope] = version;
                    return version;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                MarkL2Failed(ex, "get-version", key);
            }
        }

        return _localVersions.GetOrAdd(scope, "0");
    }

    public async Task<string> BumpVersionAsync(string scope, string reason, CancellationToken ct)
    {
        var version = _time.GetUtcNow().UtcTicks.ToString();             // монотонно зростає — без гонок «get + 1»
        _localVersions[scope] = version;

        if (IsL2Available())
        {
            try
            {
                await _distributed.SetAsync(VersionKey(scope), Encoding.UTF8.GetBytes(version),
                        new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = _options.VersionTtl }, ct)
                    .WaitAsync(_options.L2Timeout, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                MarkL2Failed(ex, "bump-version", scope);
            }
        }

        Interlocked.Increment(ref _invalidations);
        _metrics.Invalidated(reason);
        _logger.LogInformation("Cache group {Scope} invalidated by new version {Version}, reason {Reason}", scope, version, reason);
        return version;
    }

    public CacheStatistics GetStatistics() => new(
        Interlocked.Read(ref _l1Hits), Interlocked.Read(ref _l1Misses), Interlocked.Read(ref _l2Hits),
        Interlocked.Read(ref _l2Misses), Interlocked.Read(ref _invalidations), Interlocked.Read(ref _errors), IsL2Available());

    // ------------------------------------------------------------------ внутрішнє

    private sealed record L1Entry(object Value, DateTimeOffset ExpiresAt, int Bytes);

    private static string VersionKey(string scope) => $"{scope}:version";

    private bool IsL2Available()
    {
        var retryAt = Interlocked.Read(ref _l2RetryAtTicks);
        return retryAt == 0 || _time.GetUtcNow().UtcTicks >= retryAt;
    }

    private void MarkL2Failed(Exception ex, string operation, string key)
    {
        Interlocked.Increment(ref _errors);
        _metrics.Error("L2");
        Interlocked.Exchange(ref _l2RetryAtTicks, (_time.GetUtcNow() + _options.L2RetryAfterFailure).UtcTicks);
        _logger.LogWarning(ex, "L2 cache (Redis) unavailable during {Operation} of {Key}; falling back to L1 + source for {RetrySeconds} s",
            operation, key, _options.L2RetryAfterFailure.TotalSeconds);
    }

    /// <summary>Queried = чи вдалося звернутися до Redis (лише тоді відсутність запису — промах L2, а не збій).</summary>
    private async Task<(bool Queried, byte[]? Bytes)> TryReadL2Async(string key, CancellationToken ct)
    {
        if (!IsL2Available()) return (false, null);

        var sw = Stopwatch.StartNew();
        try
        {
            var bytes = await _distributed.GetAsync(key, ct).WaitAsync(_options.L2Timeout, ct);
            _metrics.Duration("L2", "get", sw.Elapsed.TotalMilliseconds);
            return (true, bytes);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            MarkL2Failed(ex, "get", key);
            return (false, null);
        }
    }

    private T? Deserialize<T>(string key, byte[] bytes) where T : class
    {
        try { return JsonSerializer.Deserialize<T>(bytes, Json); }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Cache entry {Key} cannot be deserialized and is ignored", key);   // напр., змінилась схема DTO
            return null;
        }
    }

    private void SetL1(string key, object value, CachePolicy policy, int bytes)
    {
        if (policy.EffectiveL1Absolute is not { } absolute) return;

        var options = new MemoryCacheEntryOptions
        {
            Size = policy.Size,                                           // обов'язковий, коли заданий SizeLimit
            AbsoluteExpirationRelativeToNow = absolute,
            Priority = CacheItemPriority.Normal
        };
        if (policy.L1Sliding is { } sliding) options.SlidingExpiration = sliding < absolute ? sliding : absolute;

        options.RegisterPostEvictionCallback((_, _, reason, _) =>
        {
            if (reason is EvictionReason.Capacity or EvictionReason.Expired or EvictionReason.TokenExpired)
                _metrics.Evicted(reason.ToString());                      // Removed/Replaced — не витіснення
        });

        _memory.Set(key, new L1Entry(value, _time.GetUtcNow() + absolute, bytes), options);
    }
}
