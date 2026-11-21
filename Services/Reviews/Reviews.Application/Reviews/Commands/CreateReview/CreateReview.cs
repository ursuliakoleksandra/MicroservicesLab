using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using Reviews.Application.Common.Messaging;
using Reviews.Domain.Entities;
using Reviews.Domain.Interfaces;
using Reviews.Domain.ValueObjects;

namespace Reviews.Application.Reviews.Commands.CreateReview;

/// <summary>Створити відгук. Повертає рядковий ObjectId нового документа.</summary>
public record CreateReviewCommand(
    long ProductId, string ProductName, long AuthorId, string AuthorName, int Rating, string? Text,
    IReadOnlyList<string>? Photos = null, IReadOnlyList<string>? Pros = null, IReadOnlyList<string>? Cons = null,
    string? VideoUrl = null, int? DurationSec = null, long? OrderId = null) : ICommand<string>;

public class CreateReviewCommandHandler(IReviewRepository reviews, ILogger<CreateReviewCommandHandler> logger)
    : IRequestHandler<CreateReviewCommand, string>
{
    public async Task<string> Handle(CreateReviewCommand cmd, CancellationToken ct)
    {
        var review = new Review(cmd.ProductId, cmd.ProductName, new Author(cmd.AuthorId, cmd.AuthorName), cmd.Rating, cmd.Text);

        if (cmd.Photos is { Count: > 0 }) review.AddPhotos(cmd.Photos);
        if (cmd.Pros is not null || cmd.Cons is not null) review.SetProsAndCons(cmd.Pros, cmd.Cons);
        if (cmd.VideoUrl is not null) review.AttachVideo(cmd.VideoUrl, cmd.DurationSec ?? 0);
        if (cmd.OrderId is { } orderId) review.MarkVerifiedPurchase(orderId);

        await reviews.AddAsync(review, ct);
        logger.LogInformation("Review {ReviewId} created for product {ProductId}", review.Id, review.ProductId);
        return review.Id.ToString();
    }
}

public class CreateReviewCommandValidator : AbstractValidator<CreateReviewCommand>
{
    public CreateReviewCommandValidator(IReviewRepository reviews)
    {
        RuleFor(x => x.ProductId).GreaterThan(0);
        RuleFor(x => x.ProductName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.AuthorId).GreaterThan(0);
        RuleFor(x => x.AuthorName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Rating).InclusiveBetween(1, 5);
        RuleFor(x => x.Text).MaximumLength(2000);

        RuleFor(x => x.Photos).Must(p => p!.Count <= 10).When(x => x.Photos is not null).WithMessage("At most 10 photos");
        RuleForEach(x => x.Photos).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Pros).Must(p => p!.Count <= 10).When(x => x.Pros is not null).WithMessage("At most 10 pros");
        RuleFor(x => x.Cons).Must(p => p!.Count <= 10).When(x => x.Cons is not null).WithMessage("At most 10 cons");
        RuleFor(x => x.VideoUrl).MaximumLength(500);
        RuleFor(x => x.DurationSec).NotNull().GreaterThan(0).When(x => x.VideoUrl is not null)
            .WithMessage("DurationSec is required for a video review");
        RuleFor(x => x.OrderId).GreaterThan(0).When(x => x.OrderId is not null);

        // асинхронна валідація із запитом до MongoDB: один відгук від автора на товар
        RuleFor(x => x)
            .MustAsync(async (cmd, ct) => !await reviews.ExistsAsync(cmd.ProductId, cmd.AuthorId, ct))
            .WithName("Review")
            .WithMessage("You have already reviewed this product")
            .When(x => x.ProductId > 0 && x.AuthorId > 0);
    }
}
