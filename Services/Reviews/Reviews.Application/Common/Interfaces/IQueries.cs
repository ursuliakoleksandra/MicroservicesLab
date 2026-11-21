using Reviews.Application.Common.Models;
using Reviews.Application.Discussions;
using Reviews.Application.Reviews;

namespace Reviews.Application.Common.Interfaces;

/// <summary>Критерії вибірки відгуків; пагінація, фільтрація й сортування виконуються засобами MongoDB.</summary>
public record ReviewListCriteria(long? ProductId, string? Search, int? MinRating, string SortBy, int Page, int PageSize);

/// <summary>
/// Читання відгуків (read side CQRS). Обробники запитів Application не звертаються до MongoDB напряму —
/// вони викликають цей інтерфейс; реалізація (проєкція в DTO, aggregation pipeline, $match, skip/limit) — в Infrastructure.
/// </summary>
public interface IReviewQueries
{
    Task<ReviewDto?> GetByIdAsync(string id, CancellationToken ct);
    Task<PagedResult<ReviewDto>> GetListAsync(ReviewListCriteria criteria, CancellationToken ct);
}

public interface IDiscussionQueries
{
    Task<DiscussionDto?> GetByIdAsync(string id, CancellationToken ct);
}
