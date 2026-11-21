using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Reviews.Domain.Entities;
using Reviews.Domain.ValueObjects;
using Reviews.Infrastructure.Persistence;

namespace Reviews.Infrastructure.Seeding;

public interface IDataSeeder
{
    Task SeedAsync(CancellationToken ct = default);
}

/// <summary>
/// Ідемпотентний сідінг обговорень: документ додається, лише якщо обговорення з таким (productId, title) ще немає.
/// Дані збігаються з db/p3/seed.js (Лаб. 01) за змістом, тому повторний запуск — з будь-якого боку — дублікатів не створює.
/// </summary>
public class DiscussionSeeder(MongoDbContext context, ILogger<DiscussionSeeder> logger) : IDataSeeder
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var seeds = new[]
        {
            new Discussion(1, "Ноутбук X15", "Охолодження під навантаженням", new[] { "performance", "cooling" }),
            new Discussion(3, "Навушники Air", "Чи підходять для спорту?")
        };

        var toInsert = new List<Discussion>();
        foreach (var d in seeds)
        {
            var exists = await context.Discussions.Find(x => x.ProductId == d.ProductId && x.Title == d.Title).AnyAsync(ct);
            if (!exists) toInsert.Add(d);
        }

        if (toInsert.Count > 0) await context.Discussions.InsertManyAsync(toInsert, cancellationToken: ct);   // bulk-операція
        logger.LogInformation("Discussions seeded: {Inserted} new, {Skipped} already present", toInsert.Count, seeds.Length - toInsert.Count);
    }
}

/// <summary>Ідемпотентний сідінг відгуків: різні набори полів в одній колекції (гнучка схема) — текстовий, з pros/cons, відео, лише оцінка.</summary>
public class ReviewSeeder(MongoDbContext context, ILogger<ReviewSeeder> logger) : IDataSeeder
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var d1 = await context.Discussions.Find(x => x.ProductId == 1 && x.Title == "Охолодження під навантаженням").FirstOrDefaultAsync(ct);
        var d3 = await context.Discussions.Find(x => x.ProductId == 3 && x.Title == "Чи підходять для спорту?").FirstOrDefaultAsync(ct);

        var full = new Review(1, "Ноутбук X15", new Author(1, "Олена"), 5, "Чудовий екран");
        full.AddPhotos(new[] { "img1.jpg", "img2.jpg" });
        full.AddComment("Андрій", "Згоден");
        full.MarkVerifiedPurchase(1);
        if (d1 is not null) full.LinkDiscussion(d1.Id);

        var prosCons = new Review(1, "Ноутбук X15", new Author(2, "Андрій"), 4, "Швидкий, але гучний під навантаженням");
        prosCons.SetProsAndCons(new[] { "екран", "продуктивність" }, new[] { "шум", "вага" });

        var video = new Review(3, "Навушники Air", new Author(1, "Олена"), 5);
        video.AttachVideo("https://videos.example.com/air-review.mp4", 95);
        if (d3 is not null) video.LinkDiscussion(d3.Id);

        var ratingOnly = new Review(2, "Смартфон S10", new Author(3, "Ірина"), 3);

        var toInsert = new List<Review>();
        foreach (var review in new[] { full, prosCons, video, ratingOnly })
        {
            var f = Builders<Review>.Filter;
            var exists = await context.Reviews
                .Find(f.And(f.Eq(r => r.ProductId, review.ProductId), f.Eq("author.customerId", review.Author.CustomerId)))
                .AnyAsync(ct);
            if (!exists) toInsert.Add(review);                    // перед вставкою перевіряється наявність запису
        }

        if (toInsert.Count > 0) await context.Reviews.InsertManyAsync(toInsert, cancellationToken: ct);
        logger.LogInformation("Reviews seeded: {Inserted} new, {Skipped} already present", toInsert.Count, 4 - toInsert.Count);
    }
}

/// <summary>Запускає всі IDataSeeder у порядку реєстрації (спочатку обговорення, потім відгуки) після створення індексів.</summary>
public class DataSeedingService(IEnumerable<IDataSeeder> seeders, IConfiguration config, ILogger<DataSeedingService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        if (!config.GetValue("Seeding:Enabled", true))
        {
            logger.LogInformation("Data seeding is disabled (Seeding:Enabled = false)");
            return;
        }
        foreach (var seeder in seeders) await seeder.SeedAsync(ct);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
