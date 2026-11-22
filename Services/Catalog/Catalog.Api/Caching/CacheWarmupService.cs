using Catalog.Bll.Dtos;
using Catalog.Bll.Services;

namespace Catalog.Api.Caching;

/// <summary>
/// Прогрівання кешу (Лаб. 06, ч. 3.4): під час старту й далі періодично завантажує довідники й перші товари,
/// щоб перші запити клієнтів уже потрапляли в кеш. Збій прогрівання не зупиняє сервіс.
/// </summary>
public sealed class CacheWarmupService(IServiceScopeFactory scopes, ILogger<CacheWarmupService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);   // менше за L2 TTL довідників (1 год) і картки (10 хв)

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // міграції й сідінг виконуються до app.Run(), тож БД вже готова
        using var timer = new PeriodicTimer(Interval);
        do
        {
            await WarmUpAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task WarmUpAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var categories = await scope.ServiceProvider.GetRequiredService<ICategoryService>().GetAllAsync(ct);
            var brands = await scope.ServiceProvider.GetRequiredService<IBrandService>().GetAllAsync(ct);

            var products = scope.ServiceProvider.GetRequiredService<IProductService>();
            var firstPage = await products.GetListAsync(new ProductQuery { Page = 1, PageSize = 20 }, ct);
            foreach (var item in firstPage.Items.Take(10))
                await products.GetByIdAsync(item.Id, ct);

            logger.LogInformation("Cache warmed up: {Categories} categories, {Brands} brands, {Products} products",
                categories.Count, brands.Count, Math.Min(10, firstPage.Items.Count));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache warm-up failed; the service continues without it");
        }
    }
}
