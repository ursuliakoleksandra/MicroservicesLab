using System.Diagnostics.Metrics;
using AutoMapper;
using Catalog.Bll.Dtos;
using Catalog.Bll.Mapping;
using Catalog.Bll.Services;
using Catalog.Dal.Interfaces;
using Catalog.Domain.Entities;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Platform.Caching;
using Xunit;

namespace Catalog.Tests;

/// <summary>Cache-aside і інвалідація при записі на реальному TwoLevelCache (L1 = MemoryCache, L2 = MemoryDistributedCache).</summary>
public class CategoryCachingTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly ICategoryRepository _categories = Substitute.For<ICategoryRepository>();
    private readonly CategoryService _sut;

    public CategoryCachingTests()
    {
        _uow.Categories.Returns(_categories);
        _categories.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Category> { new("Laptops", "laptops") });

        var cache = new TwoLevelCache(
            new MemoryCache(new MemoryCacheOptions { SizeLimit = 100 }),
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
            new CacheMetrics(new Meter("Catalog.Cache.Tests")),
            NullLogger<TwoLevelCache>.Instance);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAutoMapper(cfg => cfg.AddMaps(typeof(ProductProfile).Assembly));
        var mapper = services.BuildServiceProvider().GetRequiredService<IMapper>();

        _sut = new CategoryService(_uow, mapper, NullLogger<CategoryService>.Instance, cache);
    }

    [Fact]
    public async Task SecondRead_IsServedFromCache_WithoutTouchingDatabase()
    {
        var first = await _sut.GetAllAsync(CancellationToken.None);
        var second = await _sut.GetAllAsync(CancellationToken.None);

        Assert.Single(first);
        Assert.Equal(first[0].Name, second[0].Name);
        await _categories.Received(1).GetAllAsync(Arg.Any<CancellationToken>());          // лише один похід у БД
    }

    [Fact]
    public async Task Create_InvalidatesCache_SoNextReadGoesToDatabaseAgain()
    {
        await _sut.GetAllAsync(CancellationToken.None);                                    // прогріли кеш
        _categories.ExistsByNameOrSlugAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        await _sut.CreateAsync(new CreateCategoryDto { Name = "Phones", Slug = "phones" }, CancellationToken.None);
        await _sut.GetAllAsync(CancellationToken.None);                                    // після запису — знову з БД

        await _categories.Received(2).GetAllAsync(Arg.Any<CancellationToken>());
    }
}
