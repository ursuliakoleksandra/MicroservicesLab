using Catalog.Api.Caching;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Catalog.Api.HealthChecks;

public static class CatalogHealthChecks
{
    /// <summary>
    /// Readiness-перевірки Каталогу (Лаб. 07, ч. 3): SQL Server (SELECT 1, 5 с), Redis (PING, 3 с, при збої Degraded, а не Unhealthy —
    /// кеш необов'язковий: сервіс читає з бази й лишається придатним), статистика кешу та gRPC Health Checking Protocol.
    /// </summary>
    public static IServiceCollection AddCatalogHealthChecks(this IServiceCollection services, string sqlConnectionString,
        string? redisConnectionString)
    {
        var checks = services.AddHealthChecks()
            .AddSqlServer(
                connectionString: sqlConnectionString,
                name: "catalog-db",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["database", "sql", "ready"],
                timeout: TimeSpan.FromSeconds(5))
            .AddCheck<CacheHealthCheck>("cache-stats", failureStatus: HealthStatus.Degraded, tags: ["cache", "ready"]);

        if (!string.IsNullOrWhiteSpace(redisConnectionString))
            checks.AddRedis(
                redisConnectionString,
                name: "redis-cache",
                failureStatus: HealthStatus.Degraded,              // кеш необов'язковий
                tags: ["cache", "redis", "ready"],
                timeout: TimeSpan.FromSeconds(3));

        services.AddGrpcHealthChecks(o => o.Services.MapService("", r => r.Tags.Contains("ready")));
        return services;
    }
}
