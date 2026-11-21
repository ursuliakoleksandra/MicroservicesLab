using System.Reflection;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using Reviews.Domain.Common;
using Reviews.Domain.Entities;
using Reviews.Domain.ValueObjects;

namespace Reviews.Infrastructure.Persistence;

/// <summary>
/// BSON-мапінг доменних класів БЕЗ атрибутів у Domain (BsonClassMap). Реєструється один раз при старті, до першого звернення до БД.
/// Імена елементів — camelCase, як у документах з Лаб. 01; приватні поля (_comments, _photos...) мапляться явно;
/// незмінні Value Objects створюються через конструктор (MapCreator); необов'язкові поля не записуються, якщо вони null.
/// </summary>
public static class MongoMappings
{
    private static readonly object Sync = new();
    private static bool _registered;

    public static void Register()
    {
        lock (Sync)
        {
            if (_registered) return;

            ConventionRegistry.Register("reviews-conventions", new ConventionPack
            {
                new CamelCaseElementNameConvention(),
                new IgnoreExtraElementsConvention(true),
                new IgnoreIfNullConvention(true)
            }, _ => true);

            BsonSerializer.RegisterSerializer(new RatingSerializer());
            BsonSerializer.RegisterSerializer(new ReviewTypeSerializer());

            BsonClassMap.RegisterClassMap<BaseEntity>(cm =>
            {
                cm.MapIdProperty(e => e.Id);
                cm.MapProperty(e => e.CreatedAt).SetElementName("createdAt");
                cm.MapProperty(e => e.UpdatedAt).SetElementName("updatedAt").SetIgnoreIfNull(true);
                cm.MapProperty(e => e.Version).SetElementName("version");
                cm.SetIgnoreExtraElements(true);
            });

            BsonClassMap.RegisterClassMap<Review>(cm =>
            {
                UsePrivateDefaultConstructor(cm);                                             // private Review() — для десеріалізації
                cm.MapProperty(r => r.ProductId).SetElementName("productId");
                cm.MapProperty(r => r.ProductName).SetElementName("productName");
                cm.MapProperty(r => r.Author).SetElementName("author");                       // вкладений Value Object
                cm.MapProperty(r => r.Rating).SetElementName("rating");                       // Value Object -> int
                cm.MapProperty(r => r.Type).SetElementName("type");
                cm.MapProperty(r => r.Text).SetElementName("text").SetIgnoreIfNull(true);
                cm.MapProperty(r => r.VideoUrl).SetElementName("videoUrl").SetIgnoreIfNull(true);
                cm.MapProperty(r => r.DurationSec).SetElementName("durationSec").SetIgnoreIfNull(true);
                cm.MapProperty(r => r.VerifiedPurchase).SetElementName("verifiedPurchase").SetIgnoreIfNull(true);
                cm.MapProperty(r => r.DiscussionId).SetElementName("discussionId").SetIgnoreIfNull(true);
                cm.MapField("_comments").SetElementName("comments");                          // приватне поле -> вкладений масив
                cm.MapField("_photos").SetElementName("photos").SetIgnoreIfNull(true);
                cm.MapField("_pros").SetElementName("pros").SetIgnoreIfNull(true);
                cm.MapField("_cons").SetElementName("cons").SetIgnoreIfNull(true);
                cm.SetIgnoreExtraElements(true);
            });

            BsonClassMap.RegisterClassMap<Discussion>(cm =>
            {
                UsePrivateDefaultConstructor(cm);
                cm.MapProperty(d => d.ProductId).SetElementName("productId");
                cm.MapProperty(d => d.ProductName).SetElementName("productName");
                cm.MapProperty(d => d.Title).SetElementName("title");
                cm.MapProperty(d => d.MessagesCount).SetElementName("messagesCount");
                cm.MapProperty(d => d.LastMessageAt).SetElementName("lastMessageAt").SetIgnoreIfNull(true);
                cm.MapField("_tags").SetElementName("tags").SetIgnoreIfNull(true);
                cm.SetIgnoreExtraElements(true);
            });

            // незмінні Value Objects / вкладені документи: значення читаються й передаються в конструктор
            BsonClassMap.RegisterClassMap<Author>(cm =>
            {
                cm.MapProperty(a => a.CustomerId).SetElementName("customerId");
                cm.MapProperty(a => a.DisplayName).SetElementName("displayName");
                cm.MapCreator(a => new Author(a.CustomerId, a.DisplayName));
                cm.SetIgnoreExtraElements(true);
            });

            BsonClassMap.RegisterClassMap<VerifiedPurchase>(cm =>
            {
                cm.MapProperty(v => v.OrderId).SetElementName("orderId");
                cm.MapCreator(v => new VerifiedPurchase(v.OrderId));
                cm.SetIgnoreExtraElements(true);
            });

            BsonClassMap.RegisterClassMap<Comment>(cm =>
            {
                cm.MapProperty(c => c.AuthorName).SetElementName("author");
                cm.MapProperty(c => c.Text).SetElementName("text");
                cm.MapProperty(c => c.CreatedAt).SetElementName("createdAt");
                cm.MapCreator(c => new Comment(c.AuthorName, c.Text, c.CreatedAt));
                cm.SetIgnoreExtraElements(true);
            });

            _registered = true;
        }
    }

    private static void UsePrivateDefaultConstructor<T>(BsonClassMap<T> cm)
    {
        var ctor = typeof(T).GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null)
                   ?? throw new InvalidOperationException($"{typeof(T).Name} must have a parameterless constructor for BSON deserialization");
        cm.MapConstructor(ctor);
    }
}
