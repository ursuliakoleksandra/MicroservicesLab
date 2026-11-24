using System.Diagnostics.Metrics;

namespace Catalog.Api.Metrics;

/// <summary>
/// Бізнес-метрики Каталогу (Лаб. 07, ч. 2). Singleton; Meter "Catalog.Api" (метрики кешу — окремий Meter "Catalog.Cache", Лаб. 06).
/// ТЕГИ: change.type (details | price | detail | image), stock.direction (add | remove), operation (Controller.Action), status (success | failure).
/// Ідентифікатори товарів у тегах НЕ використовуються (кардинальність).
/// </summary>
public sealed class CatalogMetrics
{
    private readonly Counter<long> _created, _updated, _stockChanged, _categoriesChanged, _deleted;
    private readonly Histogram<double> _operationDuration;
    private int _inFlight;

    public CatalogMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("Catalog.Api");

        _created = meter.CreateCounter<long>("products.created", unit: "{product}", description: "Number of created products");
        _updated = meter.CreateCounter<long>("products.updated", unit: "{product}", description: "Product updates by change type");
        _stockChanged = meter.CreateCounter<long>("products.stock.changed", unit: "{operation}", description: "Stock adjustments by direction");
        _categoriesChanged = meter.CreateCounter<long>("products.categories.changed", unit: "{operation}", description: "Category assignments and removals");
        _deleted = meter.CreateCounter<long>("products.deleted", unit: "{product}", description: "Deleted products");

        _operationDuration = meter.CreateHistogram<double>("catalog.operation.duration", unit: "ms",
            description: "Duration of API operations of the catalog service (successful and failed)");

        meter.CreateObservableGauge("catalog.operations.in_flight", () => Volatile.Read(ref _inFlight), unit: "{operation}",
            description: "API operations being processed right now");
    }

    public void ProductCreated() => _created.Add(1);
    public void ProductUpdated(string changeType) => _updated.Add(1, Tag("change.type", changeType));
    public void StockChanged(string direction) => _stockChanged.Add(1, Tag("stock.direction", direction));
    public void CategoriesChanged() => _categoriesChanged.Add(1);
    public void ProductDeleted() => _deleted.Add(1);

    public void OperationStarted() => Interlocked.Increment(ref _inFlight);

    public void OperationFinished(string operation, bool success, double milliseconds)
    {
        Interlocked.Decrement(ref _inFlight);
        _operationDuration.Record(milliseconds, Tag("operation", operation), Tag("status", success ? "success" : "failure"));
    }

    private static KeyValuePair<string, object?> Tag(string key, string value) => new(key, value);
}
