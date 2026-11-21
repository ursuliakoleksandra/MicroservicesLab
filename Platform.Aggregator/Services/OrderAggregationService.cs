using System.Diagnostics;
using Platform.Aggregator.Clients;
using Platform.Aggregator.Models;

namespace Platform.Aggregator.Services;

public interface IOrderAggregationService
{
    /// <summary>Повна картка замовлення або null, якщо замовлення не існує.</summary>
    Task<FullOrderDto?> GetFullOrderAsync(long orderId, CancellationToken ct);
}

/// <summary>
/// Aggregator Pattern: замовлення береться першим (без нього агрегувати нічого), далі каталог і відгуки опитуються
/// ПАРАЛЕЛЬНО через Task.WhenAll. Недоступність каталогу чи відгуків не «валить» запит — вона потрапляє в Warnings.
/// CancellationToken передається в усі виклики; Task.WaitAll і .Result не використовуються.
/// </summary>
public sealed class OrderAggregationService(
    IOrdersClient orders, ICatalogClient catalog, IReviewsClient reviews, ILogger<OrderAggregationService> logger)
    : IOrderAggregationService
{
    private static readonly ActivitySource Source = new("Platform.Aggregator");

    public async Task<FullOrderDto?> GetFullOrderAsync(long orderId, CancellationToken ct)
    {
        using var activity = Source.StartActivity("AggregateFullOrder");
        activity?.SetTag("order.id", orderId);

        var warnings = new List<string>();

        // основний сервіс: якщо недоступний — помилка (HttpRequestException), якщо немає замовлення — null
        var order = await TimedAsync("orders-api", $"order {orderId}", () => orders.GetOrderAsync(orderId, ct));
        if (order is null) return null;

        var productIds = order.Items.Select(i => i.ProductId).Distinct().ToList();

        var productsTask = SafeAsync("catalog-api", $"products {string.Join(",", productIds)}",
            () => catalog.GetProductsAsync(productIds, ct), warnings, ct);
        var reviewsTask = SafeAsync("reviews-api", $"reviews of products {string.Join(",", productIds)}",
            () => reviews.GetForProductsAsync(productIds, ct), warnings, ct);

        await Task.WhenAll(productsTask, reviewsTask);                         // паралельно: ~max(t1, t2), а не t1 + t2

        // узгодженість: у відповіді лише товари цього замовлення і відгуки саме на ці товари
        var allowed = productIds.ToHashSet();
        var products = (await productsTask ?? []).Where(p => allowed.Contains(p.Id)).ToList();
        var productReviews = (await reviewsTask ?? []).Where(r => allowed.Contains(r.ProductId)).ToList();

        if (await productsTask is not null)
            foreach (var missing in productIds.Except(products.Select(p => p.Id)))
                warnings.Add($"Product {missing} was not found in catalog-api");

        activity?.SetTag("aggregation.warnings", warnings.Count);
        logger.LogInformation("Order {OrderId} aggregated: {ProductsCount} products, {ReviewsCount} reviews, {WarningsCount} warnings",
            orderId, products.Count, productReviews.Count, warnings.Count);

        return new FullOrderDto(order, products, productReviews, warnings);
    }

    /// <summary>Виклик із логуванням тривалості; помилки не перехоплює.</summary>
    private async Task<T> TimedAsync<T>(string service, string what, Func<Task<T>> call)
    {
        logger.LogInformation("Calling {Service} for {What}", service, what);
        var sw = Stopwatch.StartNew();
        var result = await call();
        logger.LogInformation("{Service} responded in {Elapsed} ms", service, sw.ElapsedMilliseconds);
        return result;
    }

    /// <summary>Виклик необов'язкового сервісу: збій -> попередження й default, а не виняток.</summary>
    private async Task<T?> SafeAsync<T>(string service, string what, Func<Task<T>> call, List<string> warnings, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            return await TimedAsync(service, what, call);
        }
        catch (Exception ex) when (!(ex is OperationCanceledException && ct.IsCancellationRequested))
        {
            // збій сервісу (HttpRequestException, timeout, відкритий circuit breaker) -> часткова відповідь.
            // Якщо скасував клієнт (ct) — виняток іде далі, щоб зупинити решту роботи.
            logger.LogWarning(ex, "{Service} is unavailable after {Elapsed} ms: {Reason}", service, sw.ElapsedMilliseconds, ex.Message);
            lock (warnings) warnings.Add($"{service} is unavailable");
            return default;
        }
    }
}
