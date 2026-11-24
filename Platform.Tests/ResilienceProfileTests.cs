using Platform.Aggregator.Resilience;
using Xunit;

namespace Platform.Tests;

/// <summary>Профілі resilience мають бути узгодженими: інакше застосунок не запуститься (валідація опцій Polly) або повтори не встигнуть виконатися.</summary>
public class ResilienceProfileTests
{
    public static IEnumerable<object[]> Profiles => ServiceProfiles.All.Select(p => new object[] { p });

    [Theory]
    [MemberData(nameof(Profiles))]
    public void BreakerSamplingWindow_IsAtLeastTwiceTheAttemptTimeout(ServiceResilienceProfile p)
    {
        Assert.True(ServiceResilienceProfile.SamplingDuration >= p.AttemptTimeout * 2);
        Assert.True(ServiceResilienceProfile.SamplingDuration >= (p.GrpcDeadline + TimeSpan.FromSeconds(1)) * 2);   // для gRPC-варіанта
    }

    [Theory]
    [MemberData(nameof(Profiles))]
    public void TotalTimeout_IsGreaterThanAttemptTimeout_AndFitsAtLeastTheFirstRetry(ServiceResilienceProfile p)
    {
        Assert.True(p.TotalTimeout > p.AttemptTimeout);
        Assert.True(p.HttpRetries >= 1);                              // MaxRetryAttempts у Polly має бути ≥ 1
        Assert.InRange(p.GrpcMaxAttempts, 2, 5);                      // gRPC дозволяє максимум 5 спроб разом із першою
        Assert.True(p.GrpcDeadline > p.GrpcInitialBackoff);
    }

    [Fact]
    public void CriticalServices_GetMoreRetriesAndMoreSensitiveBreaker_ThanNonCritical()
    {
        var critical = ServiceProfiles.Catalog;
        var nonCritical = ServiceProfiles.Reviews;

        Assert.True(critical.Critical);
        Assert.False(nonCritical.Critical);
        Assert.True(critical.HttpRetries > nonCritical.HttpRetries);
        Assert.True(critical.GrpcMaxAttempts > nonCritical.GrpcMaxAttempts);
        Assert.True(critical.BreakerFailureRatio < nonCritical.BreakerFailureRatio);   // менший поріг = чутливіший
        Assert.True(nonCritical.TotalTimeout < critical.TotalTimeout);                 // некритичний швидко здається
    }
}
