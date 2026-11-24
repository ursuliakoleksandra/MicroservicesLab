using System.Diagnostics.Metrics;

namespace Platform.Aggregator.Metrics;

/// <summary>
/// Бізнес-метрики Aggregator (Meter "Platform.Aggregator"; Singleton).
/// Теги з НИЗЬКОЮ кардинальністю: result (full|partial|not_found|error), dependency (3 сервіси), reason (unavailable|timeout|error).
/// Ідентифікатори замовлень і користувачів у тегах не використовуються — кожна комбінація тегів це окремий часовий ряд.
/// </summary>
public sealed class AggregatorMetrics
{
    private readonly Counter<long> _requests, _dependencyFailures;
    private readonly Histogram<double> _duration;
    private int _inFlight;

    public AggregatorMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("Platform.Aggregator");
        _requests = meter.CreateCounter<long>("aggregator.requests", unit: "{request}", description: "Aggregated order requests by result");
        _dependencyFailures = meter.CreateCounter<long>("aggregator.dependency.failures", unit: "{failure}",
            description: "Failed calls to downstream services (partial responses)");
        _duration = meter.CreateHistogram<double>("aggregator.operation.duration", unit: "ms",
            description: "Duration of the aggregation operation, including failed ones");
        meter.CreateObservableGauge("aggregator.requests.in_flight", () => Volatile.Read(ref _inFlight), unit: "{request}",
            description: "Aggregations being processed right now");
    }

    public void Started() => Interlocked.Increment(ref _inFlight);

    /// <summary>Записується для ВСІХ виконань, включно з невдалими, інакше картина затримок викривлена.</summary>
    public void Finished(string result, bool fromCache, double milliseconds)
    {
        Interlocked.Decrement(ref _inFlight);
        _requests.Add(1, new KeyValuePair<string, object?>("result", result));
        _duration.Record(milliseconds,
            new KeyValuePair<string, object?>("result", result),
            new KeyValuePair<string, object?>("cache.hit", fromCache));
    }

    public void DependencyFailed(string dependency, string reason) =>
        _dependencyFailures.Add(1,
            new KeyValuePair<string, object?>("dependency", dependency),
            new KeyValuePair<string, object?>("reason", reason));
}
