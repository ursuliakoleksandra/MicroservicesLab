using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Platform.Messaging.Consuming;
using Platform.Messaging.Events;
using Reviews.Infrastructure.Persistence;

namespace Reviews.Infrastructure.Messaging;

/// <summary>
/// product.renamed → оновлює денормалізоване поле productName у ВСІХ документах товару (політика дублювання, Лаб. 01):
/// reviews, discussions і проєкції productRatings. Повідомлення колекції messages назви товару не містять.
///
/// Ідемпотентність без транзакції: MongoDB у режимі standalone (Aspire) не підтримує багатодокументні транзакції,
/// тому порядок такий: (1) якщо _id = EventId уже є в processedEvents → Duplicate; (2) оновлення — це «встановити значення»
/// ($set), а не «змінити на»: повторне застосування дає той самий результат; (3) після успіху вставляється запис про обробку
/// (унікальний _id). Збій між (2) і (3) призведе лише до повторного, безпечного виконання (2).
/// Порядок подій: поле productNameChangedAt зберігає час застосованої зміни; застаріла подія (OccurredAt не новіший)
/// документ не змінює.
/// </summary>
public sealed class ProductRenamedHandler(MongoDbContext db, ILogger<ProductRenamedHandler> logger) : IEventHandler<ProductRenamedEvent>
{
    public async Task<HandlingResult> HandleAsync(ProductRenamedEvent e, CancellationToken ct)
    {
        var eventKey = e.EventId.ToString();

        if (await db.ProcessedEvents.Find(Builders<BsonDocument>.Filter.Eq("_id", eventKey)).AnyAsync(ct))
        {
            logger.LogInformation("Duplicate {EventType} {EventId} for product {ProductId} (CorrelationId {CorrelationId}): already processed, skipped",
                nameof(ProductRenamedEvent), e.EventId, e.ProductId, e.CorrelationId);
            return HandlingResult.Duplicate;
        }

        var name = e.NewName.Trim();
        if (name.Length == 0)
            throw new PermanentFailureException($"Product {e.ProductId}: new name is empty");

        var at = e.OccurredAt;

        var reviews = await db.Reviews.UpdateManyAsync(
            Builders<Domain.Entities.Review>.Filter.And(
                Builders<Domain.Entities.Review>.Filter.Eq(r => r.ProductId, e.ProductId),
                NewerThan<Domain.Entities.Review>(at)),
            Builders<Domain.Entities.Review>.Update.Set("productName", name).Set("productNameChangedAt", at),
            cancellationToken: ct);

        var discussions = await db.Discussions.UpdateManyAsync(
            Builders<Domain.Entities.Discussion>.Filter.And(
                Builders<Domain.Entities.Discussion>.Filter.Eq(d => d.ProductId, e.ProductId),
                NewerThan<Domain.Entities.Discussion>(at)),
            Builders<Domain.Entities.Discussion>.Update.Set("productName", name).Set("productNameChangedAt", at),
            cancellationToken: ct);

        var ratings = await db.ProductRatings.UpdateOneAsync(                    // _id проєкції = productId
            Builders<BsonDocument>.Filter.And(Builders<BsonDocument>.Filter.Eq("_id", e.ProductId), NewerThan<BsonDocument>(at)),
            Builders<BsonDocument>.Update.Set("productName", name).Set("productNameChangedAt", at),
            cancellationToken: ct);

        try
        {
            await db.ProcessedEvents.InsertOneAsync(
                new BsonDocument { { "_id", eventKey }, { "eventType", nameof(ProductRenamedEvent) }, { "processedAt", DateTime.UtcNow } },
                cancellationToken: ct);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            // паралельна обробка тієї ж події іншим екземпляром: результат однаковий, це не помилка
        }

        logger.LogInformation(
            "Product {ProductId} renamed '{OldName}' -> '{NewName}' replicated: reviews={Reviews}, discussions={Discussions}, ratings={Ratings} (EventId {EventId}, CorrelationId {CorrelationId})",
            e.ProductId, e.OldName, name, reviews.ModifiedCount, discussions.ModifiedCount, ratings.ModifiedCount, e.EventId, e.CorrelationId);
        return HandlingResult.Processed;
    }

    // документ ще не мав застосованої зміни назви або вона старіша за поточну подію
    private static FilterDefinition<T> NewerThan<T>(DateTime at) => Builders<T>.Filter.Or(
        Builders<T>.Filter.Exists("productNameChangedAt", false),
        Builders<T>.Filter.Lt("productNameChangedAt", at));
}
