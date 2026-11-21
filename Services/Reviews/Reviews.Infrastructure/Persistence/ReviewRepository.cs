using MongoDB.Bson;
using MongoDB.Driver;
using Reviews.Domain.Entities;
using Reviews.Domain.Exceptions;
using Reviews.Domain.Interfaces;

namespace Reviews.Infrastructure.Persistence;

/// <summary>Запис відгуків (MongoDB Driver) з оптимістичною конкурентністю за полем version.</summary>
public class ReviewRepository(MongoDbContext context) : IReviewRepository
{
    private readonly IMongoCollection<Review> _reviews = context.Reviews;

    public async Task<Review?> GetByIdAsync(ObjectId id, CancellationToken ct = default)
        => await _reviews.Find(r => r.Id == id).FirstOrDefaultAsync(ct);

    public Task<bool> ExistsAsync(long productId, long authorId, CancellationToken ct = default)
    {
        var f = Builders<Review>.Filter;
        return _reviews.Find(f.And(f.Eq(r => r.ProductId, productId), f.Eq("author.customerId", authorId))).AnyAsync(ct);
    }

    public Task AddAsync(Review review, CancellationToken ct = default)
        => _reviews.InsertOneAsync(review, cancellationToken: ct);          // дублікат унікального індексу -> MongoWriteException (409)

    public async Task UpdateAsync(Review review, CancellationToken ct = default)
    {
        var expectedVersion = review.Version;
        review.BumpVersion();

        var result = await _reviews.ReplaceOneAsync(IdAndVersion(review.Id, expectedVersion), review, cancellationToken: ct);
        if (result.MatchedCount == 0)                                         // хтось змінив або видалив документ раніше
            throw new ConflictException($"Review {review.Id} was modified or deleted by another request");
    }

    public async Task DeleteAsync(Review review, CancellationToken ct = default)
    {
        var result = await _reviews.DeleteOneAsync(IdAndVersion(review.Id, review.Version), cancellationToken: ct);
        if (result.DeletedCount == 0)
            throw new ConflictException($"Review {review.Id} was modified or deleted by another request");
    }

    private static FilterDefinition<Review> IdAndVersion(ObjectId id, int expectedVersion)
    {
        var f = Builders<Review>.Filter;
        // документи, створені скриптами Лаб. 01, можуть не мати поля version — це версія 0
        var version = expectedVersion == 0
            ? f.Or(f.Eq("version", 0), f.Exists("version", false))
            : f.Eq("version", expectedVersion);
        return f.And(f.Eq("_id", id), version);
    }
}

public class DiscussionRepository(MongoDbContext context) : IDiscussionRepository
{
    private readonly IMongoCollection<Discussion> _discussions = context.Discussions;

    public async Task<Discussion?> GetByIdAsync(ObjectId id, CancellationToken ct = default)
        => await _discussions.Find(d => d.Id == id).FirstOrDefaultAsync(ct);

    public Task AddAsync(Discussion discussion, CancellationToken ct = default)
        => _discussions.InsertOneAsync(discussion, cancellationToken: ct);
}
