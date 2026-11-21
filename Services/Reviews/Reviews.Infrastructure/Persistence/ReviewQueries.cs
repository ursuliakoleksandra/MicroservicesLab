using MongoDB.Bson;
using MongoDB.Driver;
using Reviews.Application.Common.Interfaces;
using Reviews.Application.Common.Models;
using Reviews.Application.Discussions;
using Reviews.Application.Reviews;
using Reviews.Domain.Entities;

namespace Reviews.Infrastructure.Persistence;

// ---------- read-моделі: проєкція з БД відбувається одразу в ці класи, без десеріалізації доменних сутностей ----------
internal sealed class AuthorReadModel
{
    public long CustomerId { get; set; }
    public string DisplayName { get; set; } = "";
}

internal sealed class VerifiedPurchaseReadModel
{
    public long OrderId { get; set; }
}

internal sealed class CommentReadModel
{
    public string Author { get; set; } = "";
    public string Text { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

internal sealed class ReviewReadModel
{
    public ObjectId Id { get; set; }
    public long ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public AuthorReadModel? Author { get; set; }
    public string? Type { get; set; }
    public int Rating { get; set; }
    public string? Text { get; set; }
    public List<string>? Photos { get; set; }
    public List<string>? Pros { get; set; }
    public List<string>? Cons { get; set; }
    public string? VideoUrl { get; set; }
    public int? DurationSec { get; set; }
    public VerifiedPurchaseReadModel? VerifiedPurchase { get; set; }
    public ObjectId? DiscussionId { get; set; }
    public int CommentsCount { get; set; }
    public List<CommentReadModel>? Comments { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int Version { get; set; }
}

internal sealed class DiscussionReadModel
{
    public ObjectId Id { get; set; }
    public long ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string Title { get; set; } = "";
    public List<string>? Tags { get; set; }
    public int MessagesCount { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public int Version { get; set; }
}

/// <summary>
/// Read side (CQRS): проєкція одразу в DTO, фільтрація ($match, $text), сортування, skip/limit — на боці MongoDB.
/// Обробники Application викликають лише інтерфейс IReviewQueries.
/// </summary>
public class ReviewQueries(MongoDbContext context) : IReviewQueries
{
    private static readonly BsonDocument ListProjection = BuildProjection(includeComments: false);
    private static readonly BsonDocument DetailsProjection = BuildProjection(includeComments: true);

    public async Task<ReviewDto?> GetByIdAsync(string id, CancellationToken ct)
    {
        if (!ObjectId.TryParse(id, out var objectId)) return null;

        var model = await context.Reviews.Aggregate()
            .Match(Builders<Review>.Filter.Eq("_id", objectId))
            .Project<ReviewReadModel>(DetailsProjection)
            .FirstOrDefaultAsync(ct);

        return model is null ? null : ToDto(model, includeComments: true);
    }

    public async Task<PagedResult<ReviewDto>> GetListAsync(ReviewListCriteria c, CancellationToken ct)
    {
        var filter = BuildFilter(c);

        var total = await context.Reviews.CountDocumentsAsync(filter, cancellationToken: ct);         // рахує MongoDB

        var models = await context.Reviews.Aggregate()
            .Match(filter)                                                                             // $match
            .Sort(BuildSort(c.SortBy))                                                                 // $sort (підтримується індексами)
            .Skip((c.Page - 1) * c.PageSize)                                                           // $skip
            .Limit(c.PageSize)                                                                         // $limit
            .Project<ReviewReadModel>(ListProjection)                                                  // $project лише потрібних полів
            .ToListAsync(ct);

        var items = models.Select(m => ToDto(m, includeComments: false)).ToList();
        return new PagedResult<ReviewDto>(items, (int)Math.Min(total, int.MaxValue), c.Page, c.PageSize);
    }

    private static FilterDefinition<Review> BuildFilter(ReviewListCriteria c)
    {
        var f = Builders<Review>.Filter;
        var filters = new List<FilterDefinition<Review>>();

        if (c.ProductId is { } productId) filters.Add(f.Eq(r => r.ProductId, productId));
        if (c.MinRating is { } minRating) filters.Add(f.Gte("rating", minRating));
        if (!string.IsNullOrWhiteSpace(c.Search)) filters.Add(f.Text(c.Search.Trim()));                // text-індекс tx_reviews_text

        return filters.Count == 0 ? f.Empty : f.And(filters);
    }

    private static SortDefinition<Review> BuildSort(string sortBy)
    {
        var s = Builders<Review>.Sort;
        return sortBy switch
        {
            "oldest" => s.Ascending("createdAt").Ascending("_id"),
            "rating" => s.Descending("rating").Descending("createdAt"),
            _ => s.Descending("createdAt").Descending("_id")                                           // індекс {productId, createdAt: -1}
        };
    }

    private static BsonDocument BuildProjection(bool includeComments)
    {
        var projection = new BsonDocument
        {
            { "productId", 1 }, { "productName", 1 }, { "author", 1 }, { "type", 1 }, { "rating", 1 }, { "text", 1 },
            { "photos", 1 }, { "pros", 1 }, { "cons", 1 }, { "videoUrl", 1 }, { "durationSec", 1 },
            { "verifiedPurchase", 1 }, { "discussionId", 1 }, { "createdAt", 1 }, { "updatedAt", 1 }, { "version", 1 },
            // кількість коментарів рахує MongoDB; масив comments у списках не передається
            { "commentsCount", new BsonDocument("$size", new BsonDocument("$ifNull", new BsonArray { "$comments", new BsonArray() })) }
        };
        if (includeComments) projection["comments"] = 1;
        return projection;
    }

    private static ReviewDto ToDto(ReviewReadModel m, bool includeComments) => new()
    {
        Id = m.Id.ToString(),
        ProductId = m.ProductId,
        ProductName = m.ProductName,
        AuthorId = m.Author?.CustomerId ?? 0,
        AuthorName = m.Author?.DisplayName ?? "",
        Type = m.Type ?? "text",
        Rating = m.Rating,
        Text = m.Text,
        Photos = m.Photos ?? new List<string>(),
        Pros = m.Pros ?? new List<string>(),
        Cons = m.Cons ?? new List<string>(),
        VideoUrl = m.VideoUrl,
        DurationSec = m.DurationSec,
        OrderId = m.VerifiedPurchase?.OrderId,
        DiscussionId = m.DiscussionId?.ToString(),
        CommentsCount = m.CommentsCount,
        Comments = includeComments
            ? (m.Comments ?? new List<CommentReadModel>())
                .Select(c => new CommentDto { AuthorName = c.Author, Text = c.Text, CreatedAt = c.CreatedAt }).ToList()
            : null,
        CreatedAt = m.CreatedAt,
        UpdatedAt = m.UpdatedAt,
        Version = m.Version
    };
}

public class DiscussionQueries(MongoDbContext context) : IDiscussionQueries
{
    private static readonly BsonDocument Projection = new()
    {
        { "productId", 1 }, { "productName", 1 }, { "title", 1 }, { "tags", 1 }, { "messagesCount", 1 },
        { "lastMessageAt", 1 }, { "createdAt", 1 }, { "version", 1 }
    };

    public async Task<DiscussionDto?> GetByIdAsync(string id, CancellationToken ct)
    {
        if (!ObjectId.TryParse(id, out var objectId)) return null;

        var m = await context.Discussions.Aggregate()
            .Match(Builders<Discussion>.Filter.Eq("_id", objectId))
            .Project<DiscussionReadModel>(Projection)
            .FirstOrDefaultAsync(ct);

        return m is null ? null : new DiscussionDto
        {
            Id = m.Id.ToString(), ProductId = m.ProductId, ProductName = m.ProductName, Title = m.Title,
            Tags = m.Tags ?? new List<string>(), MessagesCount = m.MessagesCount, LastMessageAt = m.LastMessageAt,
            CreatedAt = m.CreatedAt, Version = m.Version
        };
    }
}
