using System.Diagnostics;
using System.Text;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Platform.Messaging.Telemetry;

/// <summary>
/// Передача контексту трасування через брокер: W3C traceparent у заголовках повідомлення.
/// Publisher вставляє контекст (Inject), consumer відновлює його (Extract) і створює Activity типу Consumer —
/// так у Dashboard утворюється наскрізний trace: HTTP → publish → consume → БД.
/// </summary>
public static class TraceContextPropagation
{
    public const string SourceName = "Platform.Messaging";
    public static readonly ActivitySource Source = new(SourceName);

    private static readonly TextMapPropagator Propagator = Propagators.DefaultTextMapPropagator;

    public static void Inject(Activity? activity, BasicProperties props)
    {
        if (activity is null) return;
        props.Headers ??= new Dictionary<string, object?>();
        Propagator.Inject(new PropagationContext(activity.Context, Baggage.Current), props.Headers,
            (headers, key, value) => headers[key] = value);
    }

    public static Activity? StartConsumerActivity(BasicDeliverEventArgs ea, string queue, string eventType)
    {
        // значення заголовків від брокера приходять як byte[]
        var parent = Propagator.Extract(default, ea.BasicProperties.Headers,
            (headers, key) => headers is not null && headers.TryGetValue(key, out var v) && v is byte[] bytes
                ? [Encoding.UTF8.GetString(bytes)]
                : []);

        var activity = Source.StartActivity($"{queue} process", ActivityKind.Consumer, parent.ActivityContext);
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination.name", queue);
        activity?.SetTag("messaging.operation", "process");
        activity?.SetTag("messaging.message.id", ea.BasicProperties.MessageId);
        activity?.SetTag("messaging.event.type", eventType);
        return activity;
    }
}
