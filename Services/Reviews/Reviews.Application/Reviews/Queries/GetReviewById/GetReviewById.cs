using FluentValidation;
using MediatR;
using MongoDB.Bson;
using Reviews.Application.Common.Interfaces;
using Reviews.Application.Common.Messaging;
using Reviews.Domain.Exceptions;

namespace Reviews.Application.Reviews.Queries.GetReviewById;

public record GetReviewByIdQuery(string Id) : IQuery<ReviewDto>;

public class GetReviewByIdQueryHandler(IReviewQueries queries) : IRequestHandler<GetReviewByIdQuery, ReviewDto>
{
    public async Task<ReviewDto> Handle(GetReviewByIdQuery query, CancellationToken ct)
        => await queries.GetByIdAsync(query.Id, ct) ?? throw new NotFoundException($"Review {query.Id} not found");
}

public class GetReviewByIdQueryValidator : AbstractValidator<GetReviewByIdQuery>
{
    public GetReviewByIdQueryValidator()
        => RuleFor(x => x.Id).Must(id => ObjectId.TryParse(id, out _)).WithMessage("Id must be a valid ObjectId");
}
