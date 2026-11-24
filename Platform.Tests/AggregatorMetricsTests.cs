using System.Diagnostics.Metrics;
using Platform.Aggregator.Metrics;
using Xunit;

namespace Platform.Tests;

public class AggregatorMetricsTests
{
    [Fact]
    public void Finished_RecordsRequestCounter_WithLowCardinalityResultTag_AndDuration()
    {
        var requests = new List<(long Value, string? Result)>();
        var durations = new List<double>();
        var factory = new TestMeterFactory();
        var metrics = new AggregatorMetrics(factory);

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Meter.Name == "Platform.Aggregator") l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            if (instrument.Name == "aggregator.requests")
                requests.Add((value, tags.ToArray().FirstOrDefault(t => t.Key == "result").Value as string));
        });
        listener.SetMeasurementEventCallback<double>((instrument, value, _, _) =>
        {
            if (instrument.Name == "aggregator.operation.duration") durations.Add(value);
        });
        listener.Start();

        metrics.Started();
        metrics.Finished("partial", fromCache: false, milliseconds: 42);

        Assert.Equal(new[] { (1L, "partial") }, requests);
        Assert.Equal(new[] { 42d }, durations);
    }
}
