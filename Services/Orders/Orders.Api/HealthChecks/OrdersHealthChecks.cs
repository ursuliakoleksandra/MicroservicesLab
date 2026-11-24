using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Orders.Api.HealthChecks;

public static class OrdersHealthChecks
{
    /// <summary>
    /// Readiness-перевірки сервісу замовлень (Лаб. 07, ч. 3): SQL Server (легкий запит SELECT 1, таймаут 5 с) та стандартний
    /// gRPC Health Checking Protocol, який відповідає на основі тих самих перевірок із тегом "ready".
    /// Клієнтські інтеграції Aspire реєструють свою перевірку автоматично; явна реєстрація потрібна для імені, тегів і статусу збою.
    /// </summary>
    public static IServiceCollection AddOrdersHealthChecks(this IServiceCollection services, string connectionString)
    {
        services.AddHealthChecks()
            .AddSqlServer(
                connectionString: connectionString,
                name: "orders-db",
                failureStatus: HealthStatus.Unhealthy,           // без бази сервіс непридатний: readiness -> 503
                tags: ["database", "sql", "ready"],
                timeout: TimeSpan.FromSeconds(5));

        // grpc.health.v1.Health: загальний статус сервісу ("") = перевірки з тегом "ready"
        services.AddGrpcHealthChecks(o => o.Services.MapService("", r => r.Tags.Contains("ready")));
        return services;
    }
}
