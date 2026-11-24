using System.Diagnostics.Metrics;

namespace Orders.Api.Metrics;

/// <summary>
/// Бізнес-метрики сервісу замовлень (Лаб. 07, ч. 2). Singleton; Meter "Orders.Api" створюється через IMeterFactory
/// і додається в OpenTelemetry (AddMeter) у ServiceDefaults.
///
/// ТЕГИ (однакові назви в усіх сервісах; значення — скінченні множини, тобто НИЗЬКА кардинальність):
///   order.status    — Submitted | AwaitingValidation | StockConfirmed | Paid | Shipped | Cancelled
///   payment.method  — Card | Cash | Transfer
///   operation       — Controller.Action (скінченний набір ендпоінтів)
///   status          — success | failure
/// Ідентифікатори замовлень і клієнтів у тегах НЕ використовуються: кожна унікальна комбінація тегів — окремий часовий ряд.
/// </summary>
public sealed class OrderMetrics
{
    private readonly Counter<long> _created, _confirmed, _paid, _shipped, _cancelled, _deleted;
    private readonly Histogram<double> _operationDuration;
    private int _inFlight;

    public OrderMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("Orders.Api");

        _created = meter.CreateCounter<long>("orders.created", unit: "{order}", description: "Number of created orders");
        _confirmed = meter.CreateCounter<long>("orders.confirmed", unit: "{order}", description: "Orders whose stock was confirmed");
        _paid = meter.CreateCounter<long>("orders.paid", unit: "{order}", description: "Paid orders");
        _shipped = meter.CreateCounter<long>("orders.shipped", unit: "{order}", description: "Shipped orders");
        _cancelled = meter.CreateCounter<long>("orders.cancelled", unit: "{order}", description: "Cancelled orders");
        _deleted = meter.CreateCounter<long>("orders.deleted", unit: "{order}", description: "Deleted orders");

        _operationDuration = meter.CreateHistogram<double>("orders.operation.duration", unit: "ms",
            description: "Duration of API operations of the orders service (successful and failed)");

        // gauge — знімок поточного стану, значення читається колбеком у момент збору метрик
        meter.CreateObservableGauge("orders.operations.in_flight", () => Volatile.Read(ref _inFlight), unit: "{operation}",
            description: "API operations being processed right now");
    }

    // лічильники збільшуються одразу після УСПІШНОЇ операції
    public void OrderCreated(string status) => _created.Add(1, Tag("order.status", status));
    public void OrderConfirmed() => _confirmed.Add(1, Tag("order.status", "StockConfirmed"));
    public void OrderPaid(string paymentMethod) => _paid.Add(1, Tag("payment.method", paymentMethod));
    public void OrderShipped() => _shipped.Add(1, Tag("order.status", "Shipped"));
    public void OrderCancelled() => _cancelled.Add(1, Tag("order.status", "Cancelled"));
    public void OrderDeleted() => _deleted.Add(1);

    public void OperationStarted() => Interlocked.Increment(ref _inFlight);

    /// <summary>Тривалість записується для ВСІХ виконань, включно з невдалими, інакше картина продуктивності викривлена.</summary>
    public void OperationFinished(string operation, bool success, double milliseconds)
    {
        Interlocked.Decrement(ref _inFlight);
        _operationDuration.Record(milliseconds, Tag("operation", operation), Tag("status", success ? "success" : "failure"));
    }

    private static KeyValuePair<string, object?> Tag(string key, string value) => new(key, value);
}
