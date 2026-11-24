using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace Platform.ServiceDefaults;

/// <summary>
/// JSON-відповідь health checks з деталями кожної перевірки (Лаб. 07, ч. 3.5): статус, опис, тривалість, теги, дані.
/// У Production деталі приховано — віддається лише загальний статус (внутрішні імена залежностей — не для зовнішніх очей).
/// </summary>
public static class HealthResponseWriter
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static Func<HttpContext, HealthReport, Task> Create(bool includeDetails) => (context, report) =>
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var body = new
        {
            status = report.Status.ToString(),
            totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 1),
            entries = includeDetails
                ? report.Entries.ToDictionary(e => e.Key, e => new
                {
                    status = e.Value.Status.ToString(),
                    description = e.Value.Description,
                    durationMs = Math.Round(e.Value.Duration.TotalMilliseconds, 1),
                    tags = e.Value.Tags,
                    data = e.Value.Data.Count == 0 ? null : e.Value.Data,
                    error = e.Value.Exception?.Message
                })
                : null
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(body, Json));
    };
}

/// <summary>Startup-перевірка: Unhealthy, доки застосунок не завершив старт (ApplicationStarted: міграції, сідінг, хостинг-сервіси).</summary>
public sealed class StartupHealthCheck : IHealthCheck
{
    private volatile bool _started;

    // якщо старт уже завершено, колбек спрацьовує негайно
    public StartupHealthCheck(IHostApplicationLifetime lifetime) => lifetime.ApplicationStarted.Register(() => _started = true);

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) =>
        Task.FromResult(_started ? HealthCheckResult.Healthy("Startup completed") : HealthCheckResult.Unhealthy("Starting"));
}
