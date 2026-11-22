using System.Diagnostics.Metrics;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Platform.Aggregator.Caching;
using Platform.Aggregator.Clients;
using Platform.Aggregator.Models;
using Platform.Aggregator.Services;
using Platform.Caching;
using Xunit;

namespace Platform.Tests;

/// <summary>Кеш агрегованої відповіді: перевірка ДО викликів сервісів; часткові відповіді не кешуються.</summary>
public class CachedAggregationTests
{
    private readonly IOrdersClient _orders = Substitute.For<IOrdersClient>();
    private readonly ICatalogClient _catalog = Substitute.For<ICatalogClient>();
    private readonly IReviewsClient _reviews = Substitute.For<IReviewsClient>();
    private readonly CachedOrderAggregationService _sut;

    public CachedAggregationTests()
    {
        var order = new OrderDto(1, 1, "Paid", DateTime.UtcNow, "Чернівці", null, 50m,
            [new OrderItemDto(1, "Ноутбук", 50m, 0m, 1, 50m)]);
        _orders.GetOrderAsync(1, Arg.Any<CancellationToken>()).Returns(order);
        _catalog.GetProductsAsync(Arg.Any<IReadOnlyCollection<long>>(), Arg.Any<CancellationToken>())
            .Returns(new List<ProductDto> { new(1, "Ноутбук", "NB", 50m, 5, true) });
        _reviews.GetForProductsAsync(Arg.Any<IReadOnlyCollection<long>>(), Arg.Any<CancellationToken>())
            .Returns(new List<ReviewDto>());

        var cache = new TwoLevelCache(new MemoryCache(new MemoryCacheOptions { SizeLimit = 10 }),
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
            new CacheMetrics(new Meter("Tests.Aggregator.Cache." + Guid.NewGuid())), NullLogger<TwoLevelCache>.Instance);
        var inner = new OrderAggregationService(_orders, _catalog, _reviews, NullLogger<OrderAggregationService>.Instance);
        _sut = new CachedOrderAggregationService(inner, cache, NullLogger<CachedOrderAggregationService>.Instance);
    }

    [Fact]
    public async Task SecondRequest_IsServedFromCache_WithoutCallingServices()
    {
        await _sut.GetFullOrderAsync(1, CancellationToken.None);
        await _sut.GetFullOrderAsync(1, CancellationToken.None);

        await _orders.Received(1).GetOrderAsync(1, Arg.Any<CancellationToken>());
        await _catalog.Received(1).GetProductsAsync(Arg.Any<IReadOnlyCollection<long>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Refresh_BypassesCache()
    {
        await _sut.GetFullOrderAsync(1, CancellationToken.None);
        await _sut.GetFullOrderAsync(1, refresh: true, CancellationToken.None);

        await _orders.Received(2).GetOrderAsync(1, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PartialResponse_IsNotCached_SoRecoveredServiceIsPickedUpImmediately()
    {
        _reviews.GetForProductsAsync(Arg.Any<IReadOnlyCollection<long>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("down"));
        var partial = await _sut.GetFullOrderAsync(1, CancellationToken.None);
        Assert.Single(partial!.Warnings);

        _reviews.GetForProductsAsync(Arg.Any<IReadOnlyCollection<long>>(), Arg.Any<CancellationToken>())
            .Returns(new List<ReviewDto> { new("r1", 1, "Олена", 5, "ok", DateTime.UtcNow) });
        var full = await _sut.GetFullOrderAsync(1, CancellationToken.None);

        Assert.Empty(full!.Warnings);
        Assert.Single(full.Reviews);
    }

    [Fact]
    public async Task Invalidate_ForcesRecalculation()
    {
        await _sut.GetFullOrderAsync(1, CancellationToken.None);
        await _sut.InvalidateAsync(1, CancellationToken.None);
        await _sut.GetFullOrderAsync(1, CancellationToken.None);

        await _orders.Received(2).GetOrderAsync(1, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void AggregatedTtl_IsShorterThanProductCardTtl()
    {
        Assert.True(AggregatorCache.FullOrder.L2Absolute <= TimeSpan.FromSeconds(60));
        Assert.Null(AggregatorCache.FullOrder.L1Absolute);
    }
}
