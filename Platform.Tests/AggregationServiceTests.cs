using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Grpc.Core;
using NSubstitute.ExceptionExtensions;
using Platform.Aggregator.Clients;
using Platform.Aggregator.Models;
using Platform.Aggregator.Services;
using Xunit;

namespace Platform.Tests;

/// <summary>Aggregator Pattern: паралельні виклики, часткові відмови, скасування. Клієнти замінено заглушками.</summary>
public class AggregationServiceTests
{
    private readonly IOrdersClient _orders = Substitute.For<IOrdersClient>();
    private readonly ICatalogClient _catalog = Substitute.For<ICatalogClient>();
    private readonly IReviewsClient _reviews = Substitute.For<IReviewsClient>();
    private readonly OrderAggregationService _sut;

    public AggregationServiceTests()
    {
        _sut = new OrderAggregationService(_orders, _catalog, _reviews, NullLogger<OrderAggregationService>.Instance,
            new Platform.Aggregator.Metrics.AggregatorMetrics(new TestMeterFactory()));

        var order = new OrderDto(1, 1, "Paid", DateTime.UtcNow, "Чернівці", null, 100m,
            [new OrderItemDto(1, "Ноутбук X15", 50m, 0m, 1, 50m), new OrderItemDto(3, "Навушники Air", 50m, 0m, 1, 50m)]);
        _orders.GetOrderAsync(1, Arg.Any<CancellationToken>()).Returns(order);

        _catalog.GetProductsAsync(Arg.Any<IReadOnlyCollection<long>>(), Arg.Any<CancellationToken>()).Returns(
            new List<ProductDto>
            {
                new(1, "Ноутбук X15", "NB-X15", 32999m, 10, true),
                new(3, "Навушники Air", "AU-AIR", 3499m, 50, true),
                new(99, "Чужий товар", "XX", 1m, 1, true)           // не належить замовленню -> має бути відфільтрований
            });

        _reviews.GetForProductsAsync(Arg.Any<IReadOnlyCollection<long>>(), Arg.Any<CancellationToken>()).Returns(
            new List<ReviewDto> { new("r1", 1, "Олена", 5, "Чудово", DateTime.UtcNow), new("r2", 77, "Хтось", 3, null, DateTime.UtcNow) });
    }

    [Fact]
    public async Task AllServicesAvailable_ReturnsConsistentFullOrder_WithoutWarnings()
    {
        var result = await _sut.GetFullOrderAsync(1, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Empty(result.Warnings);
        Assert.Equal(new[] { 1L, 3L }, result.Products.Select(p => p.Id).ToArray());      // товар 99 відкинуто
        Assert.Single(result.Reviews);                                           // відгук на товар 77 відкинуто
    }

    [Fact]
    public async Task ReviewsServiceDown_ReturnsOrderAndProducts_WithWarning_NotException()
    {
        _reviews.GetForProductsAsync(Arg.Any<IReadOnlyCollection<long>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var result = await _sut.GetFullOrderAsync(1, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2, result.Products.Count);
        Assert.Empty(result.Reviews);
        Assert.Contains("reviews-api is unavailable", result.Warnings);
    }

    [Theory]
    [InlineData(StatusCode.Unavailable, "reviews-api is unavailable")]
    [InlineData(StatusCode.DeadlineExceeded, "reviews-api timed out")]
    public async Task GrpcStatus_UnavailableOrDeadlineExceeded_BecomesWarning_NotException(StatusCode code, string expectedWarning)
    {
        _reviews.GetForProductsAsync(Arg.Any<IReadOnlyCollection<long>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new RpcException(new Status(code, "boom")));

        var result = await _sut.GetFullOrderAsync(1, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains(expectedWarning, result.Warnings);
        Assert.Equal(2, result.Products.Count);
    }

    [Fact]
    public async Task BothOptionalServicesDown_StillReturnsOrder()
    {
        _catalog.GetProductsAsync(Arg.Any<IReadOnlyCollection<long>>(), Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException());
        _reviews.GetForProductsAsync(Arg.Any<IReadOnlyCollection<long>>(), Arg.Any<CancellationToken>()).ThrowsAsync(new TaskCanceledException());

        var result = await _sut.GetFullOrderAsync(1, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2, result.Warnings.Count);
        Assert.Equal(1, result.Order.Id);
    }

    [Fact]
    public async Task UnknownOrder_ReturnsNull()
    {
        _orders.GetOrderAsync(404, Arg.Any<CancellationToken>()).Returns((OrderDto?)null);

        Assert.Null(await _sut.GetFullOrderAsync(404, CancellationToken.None));
    }

    [Fact]
    public async Task OrdersServiceDown_Propagates_BecauseThereIsNothingToAggregate()
    {
        _orders.GetOrderAsync(2, Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException());

        await Assert.ThrowsAsync<HttpRequestException>(() => _sut.GetFullOrderAsync(2, CancellationToken.None));
    }

    [Fact]
    public async Task ClientCancelled_IsNotHiddenAsWarning()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        _reviews.GetForProductsAsync(Arg.Any<IReadOnlyCollection<long>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _sut.GetFullOrderAsync(1, cts.Token));
    }
}
