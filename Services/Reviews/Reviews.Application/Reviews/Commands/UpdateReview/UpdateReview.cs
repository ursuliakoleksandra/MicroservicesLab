using AutoMapper;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using Reviews.Application.Common.Messaging;
using Reviews.Domain.Exceptions;
using Reviews.Domain.Interfaces;

namespace Reviews.Application.Reviews.Commands.UpdateReview;

/// <summary>Змінити рейтинг і текст. ExpectedVersion (з заголовка If-Match) — очікувана версія документа.</summary>
public record UpdateReviewCommand(string Id, int Rating, string? Text, int? ExpectedVersion = null) : ICommand<ReviewDto>;

public class UpdateReviewCommandHandler(IReviewRepository reviews, IMapper mapper, ILogger<UpdateReviewCommandHandler> logger)
    : IRequestHandler<UpdateReviewCommand, ReviewDto>
{
    public async Task<ReviewDto> Handle(UpdateReviewCommand cmd, CancellationToken ct)
    {
        var review = await reviews.GetByIdAsync(ObjectId.Parse(cmd.Id), ct)
                     ?? throw new NotFoundException($"Review {cmd.Id} not found");

        if (cmd.ExpectedVersion is { } expected && expected != review.Version)
            throw new ConflictException($"Review {cmd.Id} was modified (current version {review.Version}, expected {expected})");

        review.Edit(cmd.Rating, cmd.Text);
        await reviews.UpdateAsync(review, ct);                 // перевірка Version у фільтрі + BumpVersion

        logger.LogInformation("Review {ReviewId} updated, new version {Version}", review.Id, review.Version);
        return mapper.Map<ReviewDto>(review);
    }
}

public class UpdateReviewCommandValidator : AbstractValidator<UpdateReviewCommand>
{
    public UpdateReviewCommandValidator()
    {
        RuleFor(x => x.Id).Must(id => ObjectId.TryParse(id, out _)).WithMessage("Id must be a valid ObjectId");
        RuleFor(x => x.Rating).InclusiveBetween(1, 5);
        RuleFor(x => x.Text).MaximumLength(2000);
        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0).When(x => x.ExpectedVersion is not null);
    }
}
