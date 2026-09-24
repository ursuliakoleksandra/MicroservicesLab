using MongoDB.Driver;

namespace Reviews.Infrastructure;

public class ReviewsDbContext
{
    private readonly IMongoDatabase _database;

    public ReviewsDbContext(string connectionString = "mongodb://localhost:27017", string databaseName = "ReviewsDb")
    {
        var client = new MongoClient(connectionString);
        _database = client.GetDatabase(databaseName);
    }

    public IMongoCollection<ProductReview> Reviews => _database.GetCollection<ProductReview>("product_reviews");

    // Метод для початкового заповнення (Seed) та створення індексів
    public async Task SeedAndIndexDataAsync()
    {
        // 1. Створення індексу за ProductId для швидкого пошуку відгуків до товару
        var indexKeys = Builders<ProductReview>.IndexKeys.Ascending(r => r.ProductId);
        await Reviews.Indexes.CreateOneAsync(new CreateIndexModel<ProductReview>(indexKeys));

        // 2. Перевірка наявних даних
        var count = await Reviews.CountDocumentsAsync(FilterDefinition<ProductReview>.Empty);
        if (count == 0)
        {
            var seedReview = new ProductReview
            {
                ProductId = "22222222-2222-2222-2222-222222222222", // ID ноутбука з CatalogDb
                CustomerId = "33333333-3333-3333-3333-333333333333",
                CustomerName = "Олексій Покупченко",
                Rating = 5,
                Comment = "Чудовий ноутбук, дуже швидкий і якісний дисплей!",
                Likes = 12
            };

            await Reviews.InsertOneAsync(seedReview);
        }
    }
}