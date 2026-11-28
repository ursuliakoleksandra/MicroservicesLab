using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.Messaging.Events;
using Platform.Messaging.Telemetry;
using RabbitMQ.Client;

namespace Platform.Messaging.Publishing;

public sealed class RabbitMqEventPublisher(IConnection connection, MessagingMetrics metrics, ILogger<RabbitMqEventPublisher> logger)
    : IEventPublisher
{
    public async Task PublishAsync<T>(T @event, string exchange, string routingKey, CancellationToken ct)
        where T : IntegrationEvent
    {
        var eventType = typeof(T).Name;

        // Span типу Producer: у trace видно момент публікації, а його контекст іде в заголовках повідомлення
        using var activity = TraceContextPropagation.Source.StartActivity($"{exchange} publish", ActivityKind.Producer);
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination.name", exchange);
        activity?.SetTag("messaging.rabbitmq.destination.routing_key", routingKey);
        activity?.SetTag("messaging.message.id", @event.EventId.ToString());
        activity?.SetTag("messaging.event.type", eventType);

        // Канал створюється під операцію (IConnection — один на застосунок, IChannel — легкий і не потокобезпечний)
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

        var props = new BasicProperties
        {
            Persistent = true,                                  // повідомлення переживає перезапуск брокера (разом із durable-чергою)
            ContentType = "application/json",
            MessageId = @event.EventId.ToString(),
            Type = eventType,
            Timestamp = new AmqpTimestamp(new DateTimeOffset(@event.OccurredAt).ToUnixTimeSeconds()),
            Headers = new Dictionary<string, object?>
            {
                ["x-correlation-id"] = @event.CorrelationId,
                ["x-schema-version"] = @event.SchemaVersion
            }
        };
        TraceContextPropagation.Inject(Activity.Current, props);

        var body = JsonSerializer.SerializeToUtf8Bytes(@event, JsonDefaults.Options);
        await channel.BasicPublishAsync(exchange, routingKey, mandatory: false, props, body, ct);

        metrics.Published(eventType);
        logger.LogInformation("Published {EventType} {EventId} to {Exchange}/{RoutingKey} (CorrelationId {CorrelationId})",
            eventType, @event.EventId, exchange, routingKey, @event.CorrelationId);
    }
}
