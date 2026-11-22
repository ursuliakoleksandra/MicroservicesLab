namespace Platform.Caching;

/// <summary>Знімок статистики кешу (для health check і діагностики).</summary>
public sealed record CacheStatistics(
    long L1Hits, long L1Misses, long L2Hits, long L2Misses, long Invalidations, long Errors, bool L2Available)
{
    public double L1HitRatio => Ratio(L1Hits, L1Misses);
    public double L2HitRatio => Ratio(L2Hits, L2Misses);
    private static double Ratio(long hits, long misses) => hits + misses == 0 ? 0 : (double)hits / (hits + misses);
}

/// <summary>Дворівневий кеш: L1 (IMemoryCache) → L2 (IDistributedCache, Redis) → джерело даних.</summary>
public interface ITwoLevelCache
{
    /// <summary>Читає L1, потім L2 (заповнюючи L1). Повертає <c>null</c> при промаху обох рівнів.</summary>
    Task<T?> GetAsync<T>(string key, CachePolicy policy, CancellationToken ct) where T : class;

    /// <summary>Записує в обидва рівні.</summary>
    Task SetAsync<T>(string key, T value, CachePolicy policy, CancellationToken ct) where T : class;

    /// <summary>
    /// Cache-aside: L1 → L2 → <paramref name="factory"/> (база даних); кожен рівень заповнюється на зворотному шляху.
    /// <c>null</c> від factory не кешується. Збої Redis не ламають виклик — вони лише дають промах L2.
    /// </summary>
    Task<T?> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T?>> factory, CachePolicy policy, CancellationToken ct)
        where T : class;

    /// <summary>Видаляє ключ з обох рівнів; <paramref name="reason"/> потрапляє в лог і метрики.</summary>
    Task RemoveAsync(string key, string reason, CancellationToken ct);

    Task RemoveAsync(IEnumerable<string> keys, string reason, CancellationToken ct);

    /// <summary>Поточна версія групи ключів (для масової інвалідації списків: версія входить у ключ).</summary>
    Task<string> GetVersionAsync(string scope, CancellationToken ct);

    /// <summary>Нова версія групи: усі ключі зі старою версією стають недосяжними й самі зникають за TTL.</summary>
    Task<string> BumpVersionAsync(string scope, string reason, CancellationToken ct);

    CacheStatistics GetStatistics();
}

/// <summary>Кеш-«пустушка»: завжди промах, factory викликається щоразу. Для unit-тестів і запуску без кешу.</summary>
public sealed class NullTwoLevelCache : ITwoLevelCache
{
    public static readonly NullTwoLevelCache Instance = new();

    public Task<T?> GetAsync<T>(string key, CachePolicy policy, CancellationToken ct) where T : class => Task.FromResult<T?>(null);
    public Task SetAsync<T>(string key, T value, CachePolicy policy, CancellationToken ct) where T : class => Task.CompletedTask;
    public Task<T?> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T?>> factory, CachePolicy policy, CancellationToken ct)
        where T : class => factory(ct);
    public Task RemoveAsync(string key, string reason, CancellationToken ct) => Task.CompletedTask;
    public Task RemoveAsync(IEnumerable<string> keys, string reason, CancellationToken ct) => Task.CompletedTask;
    public Task<string> GetVersionAsync(string scope, CancellationToken ct) => Task.FromResult("0");
    public Task<string> BumpVersionAsync(string scope, string reason, CancellationToken ct) => Task.FromResult("0");
    public CacheStatistics GetStatistics() => new(0, 0, 0, 0, 0, 0, false);
}
