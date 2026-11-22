using System.Diagnostics.Metrics;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Platform.Caching;
using Xunit;

namespace Platform.Tests;

public class TwoLevelCacheTests
{
    private sealed record Item(int Id, string Name);

    private static readonly CachePolicy Policy = new()
    {
        L1Absolute = TimeSpan.FromMinutes(1), L2Absolute = TimeSpan.FromMinutes(10)
    };

    private static MemoryCache NewMemory() => new(new MemoryCacheOptions { SizeLimit = 100 });
    private static IDistributedCache NewDistributed() => new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
    private static CacheMetrics NewMetrics() => new(new Meter("Tests.Cache." + Guid.NewGuid()));

    private static TwoLevelCache Create(IMemoryCache? memory = null, IDistributedCache? distributed = null, TwoLevelCacheOptions? options = null)
        => new(memory ?? NewMemory(), distributed ?? NewDistributed(), NewMetrics(), NullLogger<TwoLevelCache>.Instance, options);

    [Fact]
    public async Task GetOrCreate_Miss_CallsFactoryOnce_ThenServesFromL1()
    {
        var cache = Create();
        var calls = 0;
        Task<Item?> Factory(CancellationToken _) { calls++; return Task.FromResult<Item?>(new Item(1, "A")); }

        var first = await cache.GetOrCreateAsync("k", Factory, Policy, CancellationToken.None);
        var second = await cache.GetOrCreateAsync("k", Factory, Policy, CancellationToken.None);

        Assert.Equal(first, second);
        Assert.Equal(1, calls);
        var stats = cache.GetStatistics();
        Assert.Equal(1, stats.L1Hits);
        Assert.Equal(1, stats.L1Misses);
    }

    [Fact]
    public async Task L1Empty_L2Hit_FillsL1_WithoutCallingFactory()
    {
        var distributed = NewDistributed();
        var writer = Create(distributed: distributed);
        await writer.SetAsync("k", new Item(7, "shared"), Policy, CancellationToken.None);   // «інша репліка» записала в Redis

        var otherReplica = Create(distributed: distributed);                                 // свій порожній L1, той самий L2
        var value = await otherReplica.GetOrCreateAsync<Item>("k", _ => throw new InvalidOperationException("DB must not be hit"),
            Policy, CancellationToken.None);

        Assert.Equal(new Item(7, "shared"), value);
        Assert.Equal(1, otherReplica.GetStatistics().L2Hits);
        // другий виклик — уже з L1
        await otherReplica.GetAsync<Item>("k", Policy, CancellationToken.None);
        Assert.Equal(1, otherReplica.GetStatistics().L1Hits);
    }

    [Fact]
    public async Task Remove_DropsBothLevels_AndCountsInvalidation()
    {
        var cache = Create();
        await cache.SetAsync("k", new Item(1, "A"), Policy, CancellationToken.None);

        await cache.RemoveAsync("k", "ProductUpdated", CancellationToken.None);

        Assert.Null(await cache.GetAsync<Item>("k", Policy, CancellationToken.None));
        Assert.Equal(1, cache.GetStatistics().Invalidations);
    }

    [Fact]
    public async Task NullFromFactory_IsNotCached()
    {
        var cache = Create();
        var calls = 0;
        Task<Item?> Factory(CancellationToken _) { calls++; return Task.FromResult<Item?>(null); }

        await cache.GetOrCreateAsync("k", Factory, Policy, CancellationToken.None);
        await cache.GetOrCreateAsync("k", Factory, Policy, CancellationToken.None);

        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task RedisDown_ServiceKeepsWorking_AndL2IsSkippedAfterFailure()
    {
        var broken = Substitute.For<IDistributedCache>();
        broken.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("Redis is down"));
        broken.SetAsync(Arg.Any<string>(), Arg.Any<byte[]>(), Arg.Any<DistributedCacheEntryOptions>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Redis is down"));
        var cache = Create(distributed: broken);

        var value = await cache.GetOrCreateAsync("k", _ => Task.FromResult<Item?>(new Item(1, "A")), Policy, CancellationToken.None);
        var again = await cache.GetOrCreateAsync<Item>("k", _ => throw new InvalidOperationException("served by L1"), Policy, CancellationToken.None);

        Assert.NotNull(value);
        Assert.Equal(value, again);                                     // L1 працює без Redis
        var stats = cache.GetStatistics();
        Assert.False(stats.L2Available);                                // circuit breaker: Redis пропускається
        Assert.True(stats.Errors >= 1);
        await broken.Received(1).GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());   // повторно до Redis не звертались
    }

    [Fact]
    public async Task BumpVersion_ChangesVersion_SoOldListKeysBecomeUnreachable()
    {
        var cache = Create();
        var v1 = await cache.GetVersionAsync("scope", CancellationToken.None);

        var v2 = await cache.BumpVersionAsync("scope", "ProductCreated", CancellationToken.None);

        Assert.NotEqual(v1, v2);
        Assert.Equal(v2, await cache.GetVersionAsync("scope", CancellationToken.None));
    }

    [Fact]
    public void L1Ttl_IsNeverLongerThanL2()
    {
        var policy = new CachePolicy { L1Absolute = TimeSpan.FromHours(5), L2Absolute = TimeSpan.FromMinutes(1) };

        Assert.Equal(TimeSpan.FromMinutes(1), policy.EffectiveL1Absolute);
    }
}
