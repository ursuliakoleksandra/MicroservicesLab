using Grpc.Core;
using Grpc.Health.V1;
using Grpc.Net.ClientFactory;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Platform.Aggregator.HealthChecks;

/// <summary>
/// Перевірка залежності через стандартний gRPC Health Checking Protocol (grpc.health.v1.Health/Check).
/// Короткий дедлайн (2 с) — швидко виявити недоступність; перевірка ідемпотентна й без побічних ефектів.
/// RpcException -> збій із настроюваним статусом: для критичних сервісів Unhealthy, для некритичних — Degraded.
/// </summary>
public sealed class GrpcDependencyHealthCheck(
    GrpcClientFactory factory, ILogger<GrpcDependencyHealthCheck> logger, string clientName, string serviceName, HealthStatus failureStatus)
    : IHealthCheck
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(2);

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var client = factory.CreateClient<Health.HealthClient>(clientName);
            var reply = await client.CheckAsync(new HealthCheckRequest(), deadline: DateTime.UtcNow + Deadline, cancellationToken: cancellationToken);

            return reply.Status == HealthCheckResponse.Types.ServingStatus.Serving
                ? HealthCheckResult.Healthy($"{serviceName} is serving")
                : new HealthCheckResult(failureStatus, $"{serviceName} status: {reply.Status}");
        }
        catch (RpcException ex)
        {
            logger.LogWarning(ex, "{Service} gRPC health check failed with {GrpcStatus}", serviceName, ex.StatusCode);
            return new HealthCheckResult(failureStatus, $"{serviceName} is unavailable ({ex.StatusCode})", ex);
        }
    }
}
