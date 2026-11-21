using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Reviews.Infrastructure.Persistence;

/// <summary>Health check підключення до MongoDB (команда ping). Детально health checks — у Лаб. 07.</summary>
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
