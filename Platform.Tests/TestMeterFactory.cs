using System.Diagnostics.Metrics;

namespace Platform.Tests;

/// <summary>Мінімальна IMeterFactory для тестів: кожен виклик створює окремий Meter (без розподілу між тестами).</summary>
public sealed class TestMeterFactory : IMeterFactory
{
    private readonly List<Meter> _meters = [];

    public Meter Create(MeterOptions options)
    {
        var meter = new Meter(options.Name, options.Version, options.Tags);
        _meters.Add(meter);
        return meter;
    }

    public void Dispose() => _meters.ForEach(m => m.Dispose());
}
