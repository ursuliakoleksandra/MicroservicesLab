using AutoMapper;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using NSubstitute;
using Reviews.Application;
using Reviews.Application.Common.Interfaces;
using Reviews.Application.Common.Mappings;
using Reviews.Application.Reviews;
using Reviews.Application.Reviews.Commands.CreateReview;
using Reviews.Application.Reviews.Commands.UpdateReview;
using Reviews.Application.Reviews.Queries.GetReviewById;
using Reviews.Application.Reviews.Queries.GetReviewsList;
using Reviews.Domain.Entities;
using Reviews.Domain.Exceptions;
using Reviews.Domain.Interfaces;
using Reviews.Domain.ValueObjects;
using Xunit;
using ValidationException = FluentValidation.ValidationException;

namespace Reviews.Tests;

/// <summary>Тести через справжній MediatR pipeline (Logging -> Validation -> Performance -> обробник) із заглушками репозиторіїв.</summary>
public class PipelineTests
{
    private readonly IReviewRepository _repo = Substitute.For<IReviewRepository>();
    private readonly IReviewQueries _queries = Substitute.For<IReviewQueries>();
    private readonly ServiceProvider _provider;

    public PipelineTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication(new ConfigurationBuilder().Build());
        services.AddAutoMapper(cfg => cfg.AddMaps(typeof(ReviewProfile).Assembly));
        services.AddSingleton(_repo);
        services.AddSingleton(_queries);
        services.AddSingleton(Substitute.For<IDiscussionRepository>());
        services.AddSingleton(Substitute.For<IDiscussionQueries>());
        _provider = services.BuildServiceProvider();
    }

    private Task<T> Send<T>(IRequest<T> request)
    {
        using var scope = _provider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ISender>().Send(request);
    }

    private static CreateReviewCommand ValidCommand(int rating = 5) =>
        new(42, "Ноутбук X15", 17, "Олена", rating, "Чудовий екран");

    [Fact]
    public void MappingConfiguration_IsValid()
        => _provider.GetRequiredService<IConfigurationProvider>().AssertConfigurationIsValid();

    [Fact]
    public async Task Create_WithRatingTen_IsRejectedByValidation_AndHandlerIsNotInvoked()
    {
        await Assert.ThrowsAsync<ValidationException>(() => Send(ValidCommand(rating: 10)));

        await _repo.DidNotReceive().AddAsync(Arg.Any<Review>(), Arg.Any<CancellationToken>());   // обробник не виконувався
    }

    [Fact]
    public async Task Create_CollectsAllValidationErrorsTogether()
    {
        var bad = new CreateReviewCommand(0, "", 0, "", 10, null);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => Send(bad));

        Assert.True(ex.Errors.Count() >= 4);
    }

    [Fact]
    public async Task Create_WhenAuthorAlreadyReviewedProduct_IsRejectedByAsyncValidator()
    {
        _repo.ExistsAsync(42, 17, Arg.Any<CancellationToken>()).Returns(true);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => Send(ValidCommand()));

        Assert.Contains(ex.Errors, e => e.ErrorMessage == "You have already reviewed this product");
        await _repo.DidNotReceive().AddAsync(Arg.Any<Review>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_Valid_ReturnsObjectIdString_AndAddsReview()
    {
        var id = await Send(ValidCommand());

        Assert.True(ObjectId.TryParse(id, out _));
        await _repo.Received(1).AddAsync(Arg.Any<Review>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetById_WithInvalidObjectId_IsRejectedByValidation_WithoutCallingQueries()
    {
        await Assert.ThrowsAsync<ValidationException>(() => Send(new GetReviewByIdQuery("not-an-object-id")));
        await _queries.DidNotReceive().GetByIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetById_Unknown_ThrowsNotFound()
    {
        _queries.GetByIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((ReviewDto?)null);
        await Assert.ThrowsAsync<NotFoundException>(() => Send(new GetReviewByIdQuery(ObjectId.GenerateNewId().ToString())));
    }

    [Fact]
    public async Task GetList_WithPageSizeAboveLimit_IsRejected()
        => await Assert.ThrowsAsync<ValidationException>(() => Send(new GetReviewsListQuery(null, 1, 500)));

    [Fact]
    public async Task Update_WithStaleVersion_ThrowsConflict_AndDoesNotWrite()
    {
        var review = new Review(42, "Ноутбук X15", new Author(17, "Олена"), 5, "Текст");
        _repo.GetByIdAsync(Arg.Any<ObjectId>(), Arg.Any<CancellationToken>()).Returns(review);

        await Assert.ThrowsAsync<ConflictException>(() =>
            Send(new UpdateReviewCommand(review.Id.ToString(), 4, "Нове", ExpectedVersion: 3)));   // у документа версія 0

        await _repo.DidNotReceive().UpdateAsync(Arg.Any<Review>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_WithCurrentVersion_UpdatesAndReturnsDto()
    {
        var review = new Review(42, "Ноутбук X15", new Author(17, "Олена"), 5, "Текст");
        _repo.GetByIdAsync(Arg.Any<ObjectId>(), Arg.Any<CancellationToken>()).Returns(review);

        var dto = await Send(new UpdateReviewCommand(review.Id.ToString(), 3, "Змінений текст", ExpectedVersion: 0));

        Assert.Equal(3, dto.Rating);
        Assert.Equal(review.Id.ToString(), dto.Id);                    // ObjectId -> string
        await _repo.Received(1).UpdateAsync(review, Arg.Any<CancellationToken>());
    }
}
