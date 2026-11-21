using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Reviews.Domain.Entities;
using Reviews.Domain.ValueObjects;
using Reviews.Infrastructure.Persistence;
using Xunit;

namespace Reviews.Tests;

/// <summary>BsonClassMap перевіряється без MongoDB: документ має структуру з Лаб. 01 і не містить внутрішніх полів .NET.</summary>
public class BsonMappingTests
{
    public BsonMappingTests() => MongoMappings.Register();

    [Fact]
    public void Review_IsSerialized_AsCamelCaseDocument_WithNestedValueObjects_AndWithoutInternalFields()
    {
        var review = new Review(42, "Ноутбук X15", new Author(17, "Олена"), 5, "Чудовий екран");
        review.AddPhotos(new[] { "img1.jpg", "img2.jpg" });
        review.AddComment("Андрій", "Згоден");

        var doc = review.ToBsonDocument();

        foreach (var field in new[] { "_id", "productId", "productName", "author", "rating", "type", "text", "photos", "comments", "createdAt", "version" })
            Assert.True(doc.Contains(field), $"missing field '{field}'");

        Assert.False(doc.Contains("_comments"));                      // приватне поле не витікає
        Assert.False(doc.Contains("Comments"));
        Assert.False(doc.Contains("ProductId"));
        Assert.False(doc.Contains("videoUrl"));                       // необов'язкові поля відсутні, якщо не задані
        Assert.False(doc.Contains("pros"));

        Assert.Equal(BsonType.Int32, doc["rating"].BsonType);          // Value Object Rating -> int
        Assert.Equal("text", doc["type"].AsString);
        Assert.Equal(17, doc["author"]["customerId"].ToInt64());
        Assert.Equal("Олена", doc["author"]["displayName"].AsString);
        Assert.Equal("Андрій", doc["comments"].AsBsonArray[0]["author"].AsString);
    }

    [Fact]
    public void Review_RoundTrip_RestoresValueObjectsAndPrivateCollections()
    {
        var review = new Review(42, "Ноутбук X15", new Author(17, "Олена"), 4, "Текст");
        review.AddComment("Андрій", "Згоден");
        review.AddPhotos(new[] { "img1.jpg" });
        review.MarkVerifiedPurchase(7);

        var restored = BsonSerializer.Deserialize<Review>(review.ToBsonDocument());

        Assert.Equal(review.Id, restored.Id);
        Assert.Equal(new Author(17, "Олена"), restored.Author);        // Value Object створено через конструктор
        Assert.Equal(4, restored.Rating.Value);
        Assert.Single(restored.Comments);
        Assert.Equal(new[] { "img1.jpg" }, restored.Photos.ToArray());
        Assert.Equal(7, restored.VerifiedPurchase!.OrderId);
    }

    [Fact]
    public void Review_CanBeDeserialized_FromLab01Document_WithoutVersionField()
    {
        var doc = new BsonDocument
        {
            { "_id", ObjectId.GenerateNewId() }, { "productId", 1 }, { "productName", "Ноутбук X15" },
            { "author", new BsonDocument { { "customerId", 2 }, { "displayName", "Андрій" } } },
            { "type", "text" }, { "rating", 4 }, { "text", "Швидкий" },
            { "pros", new BsonArray { "екран" } }, { "comments", new BsonArray() },
            { "createdAt", DateTime.UtcNow }, { "unknownField", "ignored" }
        };

        var review = BsonSerializer.Deserialize<Review>(doc);

        Assert.Equal(0, review.Version);                              // документ без version = версія 0
        Assert.Equal("Андрій", review.Author.DisplayName);
        Assert.Equal(new[] { "екран" }, review.Pros.ToArray());
    }
}
