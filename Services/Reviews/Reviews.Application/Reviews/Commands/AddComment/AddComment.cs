using AutoMapper;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using Reviews.Application.Common.Messaging;
using Reviews.Domain.Exceptions;
using Reviews.Domain.Interfaces;

namespace Reviews.Application.Reviews.Commands.AddComment;

/// <summary>Додати коментар (вкладений документ у масив comments).</summary>
public record AddCommentCommand(string ReviewId, string AuthorName, string Text, int? ExpectedVersion = null) : ICommand<ReviewDto>;

public class AddCommentCommandHandler(IReviewRepository reviews, IMapper mapper, ILogger<AddCommentCommandHandler> logger)
    : IRequestHandler<AddCommentCommand, ReviewDto>
{
    public async Task<ReviewDto> Handle(AddCommentCommand cmd, CancellationToken ct)
    {
        var review = await reviews.GetByIdAsync(ObjectId.Parse(cmd.ReviewId), ct)
                     ?? throw new NotFoundException($"Review {cmd.ReviewId} not found");

        if (cmd.ExpectedVersion is { } expected && expected != review.Version)
            throw new ConflictException($"Review {cmd.ReviewId} was modified (current version {review.Version}, expected {expected})");

        review.AddComment(cmd.AuthorName, cmd.Text);
        await reviews.UpdateAsync(review, ct);

        logger.LogInformation("Comment added to review {ReviewId}, comments: {CommentsCount}", review.Id, review.Comments.Count);
        return mapper.Map<ReviewDto>(review);
    }
}

public class AddCommentCommandValidator : AbstractValidator<AddCommentCommand>
{
    public AddCommentCommandValidator()
    {
        RuleFor(x => x.ReviewId).Must(id => ObjectId.TryParse(id, out _)).WithMessage("ReviewId must be a valid ObjectId");
        RuleFor(x => x.AuthorName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Text).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0).When(x => x.ExpectedVersion is not null);
    }
}
