using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Platform.Messaging.Consuming;
using Platform.Messaging.Publishing;
using Platform.Messaging.Telemetry;
using Platform.Messaging.Topology;
using RabbitMQ.Client;

namespace Platform.Messaging;

public static class MessagingExtensions
{
    /// <summary>Ім'я ресурсу RabbitMQ в AppHost (= ім'я рядка підключення ConnectionStrings:messaging).</summary>
    public const string ConnectionName = "messaging";

    /// <summary>
    /// Підключення до RabbitMQ через Aspire (IConnection — singleton, health check, телеметрія клієнта),
    /// метрики, publisher і оголошення топології під час старту.
    /// </summary>
    public static IHostApplicationBuilder AddMessaging(this IHostApplicationBuilder builder)
    {
        builder.AddRabbitMQClient(ConnectionName);   // реєструє IConnection (singleton) + health check "RabbitMQ.Client"

        builder.Services.AddSingleton<MessagingMetrics>();
        builder.Services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();
        builder.Services.AddHostedService<TopologyInitializer>();   // запускається першим серед hosted services

        // Readiness: зв'язок із брокером потрібен для публікації/споживання, але сервіс може працювати й без нього → Degraded
        builder.Services.AddHealthChecks().AddCheck<RabbitMqConnectionHealthCheck>("rabbitmq-connection",
            HealthStatus.Degraded, ["messaging", "ready"]);

        return builder;
    }

    /// <summary>Реєструє споживача черги (hosted service) і монітор глибини її DLQ.</summary>
    public static IServiceCollection AddEventConsumer(this IServiceCollection services, string queueName,
        Action<EventHandlerRegistry> configure, int maxRetries = 3)
    {
        var registry = new EventHandlerRegistry(services);
        configure(registry);

        var options = new ConsumerOptions { QueueName = queueName, MaxRetries = maxRetries };

        services.AddHostedService(sp => ActivatorUtilities.CreateInstance<RabbitMqEventConsumer>(sp, options, registry));
        services.AddHostedService(sp => ActivatorUtilities.CreateInstance<DlqDepthMonitor>(sp, queueName));
        return services;
    }

    internal sealed class RabbitMqConnectionHealthCheck(IConnection connection) : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default) =>
            Task.FromResult(connection.IsOpen
                ? HealthCheckResult.Healthy("RabbitMQ connection is open.")
                : new HealthCheckResult(context.Registration.FailureStatus, "RabbitMQ connection is closed."));
    }
}
