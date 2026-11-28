using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Platform.Messaging.Telemetry;

/// <summary>
/// Метрики обміну повідомленнями (Meter "Platform.Messaging", реєструється в ServiceDefaults через "Platform.*"):
/// опубліковані/оброблені події (за результатом), тривалість обробки та глибина DLQ (ObservableGauge).
/// Теги низької кардинальності: тип події, черга, результат.
/// </summary>
public sealed class MessagingMetrics
{
    public const string MeterName = "Platform.Messaging";

    private readonly Counter<long> _published;
    private readonly Counter<long> _processed;
    private readonly Histogram<double> _duration;
    private readonly ConcurrentDictionary<string, long> _dlqDepth = new();

    public MessagingMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _published = meter.CreateCounter<long>("messaging.events.published", "{event}", "Опубліковані події.");
        _processed = meter.CreateCounter<long>("messaging.events.processed", "{event}",
            "Оброблені повідомлення за результатом (processed/duplicate/ignored/retried/dead_lettered).");
        _duration = meter.CreateHistogram<double>("messaging.event.processing.duration", "ms", "Тривалість обробки повідомлення.");
        meter.CreateObservableGauge("messaging.dlq.depth",
            () => _dlqDepth.Select(kv => new Measurement<long>(kv.Value, new KeyValuePair<string, object?>("messaging.queue", kv.Key))),
            "{message}", "Кількість повідомлень у Dead Letter Queue.");
    }

    public void Published(string eventType) =>
        _published.Add(1, new TagList { { "messaging.event.type", eventType } });

    public void Processed(string queue, string eventType, string result, double elapsedMs)
    {
        var tags = new TagList { { "messaging.queue", queue }, { "messaging.event.type", eventType }, { "messaging.result", result } };
        _processed.Add(1, tags);
        _duration.Record(elapsedMs, tags);
    }

    public void SetDlqDepth(string queue, long depth) => _dlqDepth[queue] = depth;
}
