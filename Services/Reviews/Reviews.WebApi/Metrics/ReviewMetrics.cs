using System.Diagnostics.Metrics;

namespace Reviews.WebApi.Metrics;

/// <summary>
/// Бізнес-метрики сервісу відгуків (Лаб. 07, ч. 2). Singleton; Meter "Reviews.Api".
/// ТЕГИ: review.type (text | video | rating-only), operation (назва MediatR-запиту — скінченний набір), status (success | failure).
/// Ідентифікатори відгуків, товарів і авторів у тегах НЕ використовуються (кардинальність).
/// </summary>
public sealed class ReviewMetrics
{
    private readonly Counter<long> _created, _updated, _deleted, _commentsAdded;
    private readonly Histogram<double> _operationDuration;
    private int _inFlight;

    public ReviewMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("Reviews.Api");

        _created = meter.CreateCounter<long>("reviews.created", unit: "{review}", description: "Number of created reviews");
        _updated = meter.CreateCounter<long>("reviews.updated", unit: "{review}", description: "Number of updated reviews");
        _deleted = meter.CreateCounter<long>("reviews.deleted", unit: "{review}", description: "Number of deleted reviews");
        _commentsAdded = meter.CreateCounter<long>("reviews.comments.added", unit: "{comment}", description: "Comments added to reviews");

        _operationDuration = meter.CreateHistogram<double>("reviews.operation.duration", unit: "ms",
            description: "Duration of commands and queries of the reviews service (successful and failed)");

        meter.CreateObservableGauge("reviews.operations.in_flight", () => Volatile.Read(ref _inFlight), unit: "{operation}",
            description: "Commands and queries being processed right now");
    }

    public void ReviewCreated(string type) => _created.Add(1, new KeyValuePair<string, object?>("review.type", type));
    public void ReviewUpdated() => _updated.Add(1);
    public void ReviewDeleted() => _deleted.Add(1);
    public void CommentAdded() => _commentsAdded.Add(1);

    public void OperationStarted() => Interlocked.Increment(ref _inFlight);

    public void OperationFinished(string operation, bool success, double milliseconds)
    {
        Interlocked.Decrement(ref _inFlight);
        _operationDuration.Record(milliseconds,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("status", success ? "success" : "failure"));
    }
}
