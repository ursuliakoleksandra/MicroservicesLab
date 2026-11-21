using FluentValidation;
using MediatR;
using MongoDB.Bson;
using Reviews.Application.Common.Interfaces;
using Reviews.Application.Common.Messaging;
using Reviews.Domain.Exceptions;

namespace Reviews.Application.Discussions.Queries.GetDiscussionById;

public record GetDiscussionByIdQuery(string Id) : IQuery<DiscussionDto>;

public class GetDiscussionByIdQueryHandler(IDiscussionQueries queries) : IRequestHandler<GetDiscussionByIdQuery, DiscussionDto>
{
    public async Task<DiscussionDto> Handle(GetDiscussionByIdQuery query, CancellationToken ct)
        => await queries.GetByIdAsync(query.Id, ct) ?? throw new NotFoundException($"Discussion {query.Id} not found");
}

public class GetDiscussionByIdQueryValidator : AbstractValidator<GetDiscussionByIdQuery>
{
    public GetDiscussionByIdQueryValidator()
        => RuleFor(x => x.Id).Must(id => ObjectId.TryParse(id, out _)).WithMessage("Id must be a valid ObjectId");
}
