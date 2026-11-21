using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Reviews.Domain.Entities;
using Reviews.Infrastructure.Persistence;

namespace Reviews.Infrastructure.Seeding;

/// <summary>
/// Автоматично створює індекси при старті. Імена й параметри збігаються з db/p3/collections.js (Лаб. 01),
/// тому повторне створення безпечне — MongoDB не дублює вже наявні індекси.
/// </summary>
public class IndexCreationService(MongoDbContext context, ILogger<IndexCreationService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        var keys = Builders<Review>.IndexKeys;
        await context.Reviews.Indexes.CreateManyAsync(new[]
        {
            // «останні відгуки про товар» — складений індекс
            new CreateIndexModel<Review>(keys.Ascending(r => r.ProductId).Descending(r => r.CreatedAt),
                new CreateIndexOptions { Name = "ix_reviews_product_created" }),
            // один відгук від автора на товар — унікальний
            new CreateIndexModel<Review>(keys.Ascending(r => r.ProductId).Ascending("author.customerId"),
                new CreateIndexOptions { Name = "ux_reviews_product_author", Unique = true }),
            // відгуки користувача
            new CreateIndexModel<Review>(keys.Ascending("author.customerId"),
                new CreateIndexOptions { Name = "ix_reviews_author" }),
            // повнотекстовий пошук (для української немає вбудованого стеммера -> "none")
            new CreateIndexModel<Review>(keys.Text(r => r.Text),
                new CreateIndexOptions { Name = "tx_reviews_text", DefaultLanguage = "none" })
        }, ct);

        var dKeys = Builders<Discussion>.IndexKeys;
        await context.Discussions.Indexes.CreateOneAsync(
            new CreateIndexModel<Discussion>(dKeys.Ascending(d => d.ProductId).Descending(d => d.LastMessageAt),
                new CreateIndexOptions { Name = "ix_discussions_product_last" }), cancellationToken: ct);

        logger.LogInformation("MongoDB indexes ensured for collections 'reviews' and 'discussions'");
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
