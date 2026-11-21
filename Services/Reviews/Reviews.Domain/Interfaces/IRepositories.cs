using MongoDB.Bson;
using Reviews.Domain.Entities;

namespace Reviews.Domain.Interfaces;

/// <summary>Контракт доступу до даних (запис). Реалізація — в Reviews.Infrastructure (драйвер MongoDB).</summary>
public interface IReviewRepository
{
    Task<Review?> GetByIdAsync(ObjectId id, CancellationToken ct = default);

    /// <summary>Чи залишав цей автор відгук на цей товар (для асинхронної валідації).</summary>
    Task<bool> ExistsAsync(long productId, long authorId, CancellationToken ct = default);

    Task AddAsync(Review review, CancellationToken ct = default);

    /// <summary>Optimistic concurrency: оновлення застосовується лише до документа з очікуваною версією, інакше ConflictException.</summary>
    Task UpdateAsync(Review review, CancellationToken ct = default);

    /// <summary>Видалення лише документа з очікуваною версією, інакше ConflictException.</summary>
    Task DeleteAsync(Review review, CancellationToken ct = default);
}

public interface IDiscussionRepository
{
    Task<Discussion?> GetByIdAsync(ObjectId id, CancellationToken ct = default);
    Task AddAsync(Discussion discussion, CancellationToken ct = default);
}
