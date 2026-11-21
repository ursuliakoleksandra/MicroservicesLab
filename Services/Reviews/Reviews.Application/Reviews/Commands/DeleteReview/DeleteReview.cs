using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using Reviews.Application.Common.Messaging;
using Reviews.Domain.Exceptions;
using Reviews.Domain.Interfaces;

namespace Reviews.Application.Reviews.Commands.DeleteReview;

public record DeleteReviewCommand(string Id, int? ExpectedVersion = null) : ICommand;

public class DeleteReviewCommandHandler(IReviewRepository reviews, ILogger<DeleteReviewCommandHandler> logger)
    : IRequestHandler<DeleteReviewCommand>
{
    public async Task Handle(DeleteReviewCommand cmd, CancellationToken ct)
    {
        var review = await reviews.GetByIdAsync(ObjectId.Parse(cmd.Id), ct)
                     ?? throw new NotFoundException($"Review {cmd.Id} not found");

        if (cmd.ExpectedVersion is { } expected && expected != review.Version)
            throw new ConflictException($"Review {cmd.Id} was modified (current version {review.Version}, expected {expected})");

        await reviews.DeleteAsync(review, ct);
        logger.LogInformation("Review {ReviewId} deleted", review.Id);
    }
}

public class DeleteReviewCommandValidator : AbstractValidator<DeleteReviewCommand>
{
    public DeleteReviewCommandValidator()
    {
        RuleFor(x => x.Id).Must(id => ObjectId.TryParse(id, out _)).WithMessage("Id must be a valid ObjectId");
        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0).When(x => x.ExpectedVersion is not null);
    }
}
