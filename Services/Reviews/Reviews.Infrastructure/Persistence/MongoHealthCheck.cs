using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Reviews.Infrastructure.Persistence;

/// <summary>Легка перевірка MongoDB (Лаб. 07, ч. 3.2): команда ping, без читання даних; таймаут задає реєстрація (5 с).</summary>
public class MongoHealthCheck(MongoDbContext context) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext healthContext, CancellationToken ct = default)
    {
        try
        {
            await context.Database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: ct);
            return HealthCheckResult.Healthy("MongoDB is reachable");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("MongoDB is not reachable", ex);
        }
    }
}
