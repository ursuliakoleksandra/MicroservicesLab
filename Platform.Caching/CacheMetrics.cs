using System.Diagnostics.Metrics;

namespace Platform.Caching;

/// <summary>
/// Метрики ефективності кешу (Лаб. 06, ч. 6.3): cache.hits / cache.misses з тегом cache.level (L1 | L2),
/// cache.invalidations, cache.evictions, cache.errors та гістограма тривалості операцій.
/// Hit ratio рівня = hits / (hits + misses) для відповідного тегу.
/// </summary>
public sealed class CacheMetrics
{
    private readonly Counter<long> _hits, _misses, _invalidations, _evictions, _errors;
    private readonly Histogram<double> _duration;

    public CacheMetrics(Meter meter)
    {
        Meter = meter;
        _hits = meter.CreateCounter<long>("cache.hits", description: "Кількість попадань у кеш");
        _misses = meter.CreateCounter<long>("cache.misses", description: "Кількість промахів кешу");
        _invalidations = meter.CreateCounter<long>("cache.invalidations", description: "Кількість інвалідацій");
        _evictions = meter.CreateCounter<long>("cache.evictions", description: "Записи, витіснені з L1 (ємність, термін)");
        _errors = meter.CreateCounter<long>("cache.errors", description: "Збої рівня кешу (наприклад, Redis недоступний)");
        _duration = meter.CreateHistogram<double>("cache.operation.duration", unit: "ms", description: "Тривалість операції з кешем");
    }

    public Meter Meter { get; }

    public void Hit(string level) => _hits.Add(1, Level(level));
    public void Miss(string level) => _misses.Add(1, Level(level));
    public void Invalidated(string reason) => _invalidations.Add(1, new KeyValuePair<string, object?>("cache.reason", reason));
    public void Evicted(string reason) => _evictions.Add(1, new KeyValuePair<string, object?>("cache.eviction.reason", reason));
    public void Error(string level) => _errors.Add(1, Level(level));
    public void Duration(string level, string operation, double milliseconds) =>
        _duration.Record(milliseconds, Level(level), new KeyValuePair<string, object?>("cache.operation", operation));

    private static KeyValuePair<string, object?> Level(string level) => new("cache.level", level);
}
