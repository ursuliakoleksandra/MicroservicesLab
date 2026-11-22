using Microsoft.Extensions.Diagnostics.HealthChecks;
using Platform.Caching;

namespace Catalog.Api.Caching;

/// <summary>Статистика кешу в /health: hit ratio рівнів, інвалідації, збої. Redis недоступний = Degraded (сервіс працює, але повільніше).</summary>
public sealed class CacheHealthCheck(ITwoLevelCache cache) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var s = cache.GetStatistics();
        var data = new Dictionary<string, object>
        {
            ["l1.hits"] = s.L1Hits, ["l1.misses"] = s.L1Misses, ["l1.hitRatio"] = Math.Round(s.L1HitRatio, 3),
            ["l2.hits"] = s.L2Hits, ["l2.misses"] = s.L2Misses, ["l2.hitRatio"] = Math.Round(s.L2HitRatio, 3),
            ["invalidations"] = s.Invalidations, ["errors"] = s.Errors, ["l2.available"] = s.L2Available
        };

        return Task.FromResult(s.L2Available
            ? HealthCheckResult.Healthy("Cache is operating", data)
            : HealthCheckResult.Degraded("Redis (L2) is unavailable: using L1 + database", data: data));
    }
}
