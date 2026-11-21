using FluentValidation;
using MediatR;
using Reviews.Application.Common.Interfaces;
using Reviews.Application.Common.Messaging;
using Reviews.Application.Common.Models;

namespace Reviews.Application.Reviews.Queries.GetReviewsList;

/// <summary>Список відгуків: фільтр за товаром, повнотекстовий пошук, мінімальний рейтинг, сортування (newest | oldest | rating), пагінація.</summary>
public record GetReviewsListQuery(long? ProductId, int Page = 1, int PageSize = 20,
    string? Search = null, int? MinRating = null, string SortBy = "newest") : IQuery<PagedResult<ReviewDto>>;

public class GetReviewsListQueryHandler(IReviewQueries queries)
    : IRequestHandler<GetReviewsListQuery, PagedResult<ReviewDto>>
{
    public Task<PagedResult<ReviewDto>> Handle(GetReviewsListQuery q, CancellationToken ct)
        => queries.GetListAsync(
            new ReviewListCriteria(q.ProductId, q.Search, q.MinRating, q.SortBy.ToLowerInvariant(), q.Page, Math.Clamp(q.PageSize, 1, 100)), ct);
}

public class GetReviewsListQueryValidator : AbstractValidator<GetReviewsListQuery>
{
    private static readonly string[] SortFields = ["newest", "oldest", "rating"];

    public GetReviewsListQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.ProductId).GreaterThan(0).When(x => x.ProductId is not null);
        RuleFor(x => x.MinRating).InclusiveBetween(1, 5).When(x => x.MinRating is not null);
        RuleFor(x => x.Search).MaximumLength(100);
        RuleFor(x => x.SortBy).Must(s => SortFields.Contains(s.ToLowerInvariant()))
            .WithMessage("SortBy must be one of: newest, oldest, rating");
    }
}
