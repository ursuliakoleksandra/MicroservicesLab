using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Platform.Caching;

public static class CacheServiceCollectionExtensions
{
    /// <summary>
    /// Реєструє <see cref="ITwoLevelCache"/> та метрики з Meter <paramref name="meterName"/> (його ж додайте в OpenTelemetry: AddMeter).
    /// Потрібні також IMemoryCache (AddMemoryCache із SizeLimit) та IDistributedCache (Redis або AddDistributedMemoryCache).
    /// </summary>
    public static IServiceCollection AddTwoLevelCache(this IServiceCollection services, string meterName,
        Action<TwoLevelCacheOptions>? configure = null)
    {
        var options = new TwoLevelCacheOptions();
        configure?.Invoke(options);

        services.TryAddSingleton(options);
        services.TryAddSingleton(sp => new CacheMetrics(sp.GetRequiredService<IMeterFactory>().Create(meterName)));
        services.TryAddSingleton<ITwoLevelCache, TwoLevelCache>();
        return services;
    }
}
