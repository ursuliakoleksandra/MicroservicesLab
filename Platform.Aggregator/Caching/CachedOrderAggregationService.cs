using System.Diagnostics;
using Platform.Aggregator.Metrics;
using Platform.Aggregator.Models;
using Platform.Aggregator.Services;
using Platform.Caching;

namespace Platform.Aggregator.Caching;

/// <summary>Ключі й TTL кешу агрегованих відповідей (префікс aggregator: ізолює їх у спільному Redis).</summary>
public static class AggregatorCache
{
    public static string FullOrderKey(long orderId) => $"aggregator:order:{orderId}:full";

    /// <summary>
    /// Лише Redis (L1 вимкнено), короткий TTL 30 с: агрегована відповідь не повинна виглядати «свіжішою»,
    /// ніж дані, з яких її зібрано, — тому її TTL коротший за кеші окремих сервісів (картка товару — до 10 хв).
    /// </summary>
    public static readonly CachePolicy FullOrder = new() { L1Absolute = null, L2Absolute = TimeSpan.FromSeconds(30) };
}

/// <summary>
/// Декоратор: кеш перевіряється ДО gRPC-викликів. Часткові відповіді (є попередження) не кешуються —
/// інакше тимчасова відмова сервісу «застрягла б» у кеші на час TTL.
/// </summary>
public sealed class CachedOrderAggregationService(
    OrderAggregationService inner, ITwoLevelCache cache, ILogger<CachedOrderAggregationService> logger, AggregatorMetrics metrics)
    : IOrderAggregationService
{
    public async Task<FullOrderDto?> GetFullOrderAsync(long orderId, CancellationToken ct) =>
        await GetFullOrderAsync(orderId, refresh: false, ct);

    public async Task<FullOrderDto?> GetFullOrderAsync(long orderId, bool refresh, CancellationToken ct)
    {
        metrics.Started();
        var sw = Stopwatch.StartNew();
        var result = "error";
        var fromCache = false;
        try
        {
            var key = AggregatorCache.FullOrderKey(orderId);

            if (!refresh)
            {
                var cached = await cache.GetAsync<FullOrderDto>(key, AggregatorCache.FullOrder, ct);
                if (cached is not null)
                {
                    logger.LogInformation("Aggregated order {OrderId} served from cache {Key}", orderId, key);
                    fromCache = true;
                    result = "full";
                    return cached;
                }
            }

            var full = await inner.GetFullOrderAsync(orderId, ct);                       // паралельні gRPC-виклики
            result = full is null ? "not_found" : full.Warnings.Count == 0 ? "full" : "partial";

            if (full is not null && full.Warnings.Count == 0)
                await cache.SetAsync(key, full, AggregatorCache.FullOrder, ct);
            else if (full is not null)
                logger.LogInformation("Partial aggregated order {OrderId} ({WarningsCount} warnings) is not cached", orderId, full.Warnings.Count);

            return full;
        }
        finally
        {
            metrics.Finished(result, fromCache, sw.Elapsed.TotalMilliseconds);          // і для невдалих, і для частково успішних
        }
    }

    /// <summary>Ручна інвалідація (після змін у вихідних даних, коли не можна чекати 30 с).</summary>
    public Task InvalidateAsync(long orderId, CancellationToken ct) =>
        cache.RemoveAsync(AggregatorCache.FullOrderKey(orderId), "SourceDataChanged", ct);
}
