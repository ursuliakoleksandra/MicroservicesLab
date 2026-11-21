using System.Net;
using Platform.Aggregator.Models;

namespace Platform.Aggregator.Clients;

public interface IOrdersClient
{
    /// <summary>Замовлення або null, якщо orders-api відповів 404.</summary>
    Task<OrderDto?> GetOrderAsync(long id, CancellationToken ct);
}

public interface ICatalogClient
{
    /// <summary>Знайдені товари (неіснуючі Id пропускаються). Недоступність сервісу — HttpRequestException.</summary>
    Task<IReadOnlyList<ProductDto>> GetProductsAsync(IReadOnlyCollection<long> ids, CancellationToken ct);
}

public interface IReviewsClient
{
    /// <summary>Останні відгуки на кожен із товарів. Недоступність сервісу — HttpRequestException.</summary>
    Task<IReadOnlyList<ReviewDto>> GetForProductsAsync(IReadOnlyCollection<long> productIds, CancellationToken ct);
}

// Typed HttpClients: BaseAddress на кшталт "https+http://orders-api" задається в Program.cs (service discovery);
// CorrelationId додає CorrelationIdHandler із ServiceDefaults — для всіх клієнтів автоматично.
public sealed class OrdersClient(HttpClient http) : IOrdersClient
{
    public async Task<OrderDto?> GetOrderAsync(long id, CancellationToken ct)
    {
        using var response = await http.GetAsync($"/api/orders/{id}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<OrderDto>(ct);
    }
}

public sealed class CatalogClient(HttpClient http) : ICatalogClient
{
    public async Task<IReadOnlyList<ProductDto>> GetProductsAsync(IReadOnlyCollection<long> ids, CancellationToken ct)
    {
        // незалежні запити по кожному товару — теж паралельно
        var tasks = ids.Select(id => GetOneAsync(id, ct)).ToList();
        var results = await Task.WhenAll(tasks);
        return results.Where(p => p is not null).Select(p => p!).ToList();
    }

    private async Task<ProductDto?> GetOneAsync(long id, CancellationToken ct)
    {
        using var response = await http.GetAsync($"/api/products/{id}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProductDto>(ct);
    }
}

public sealed class ReviewsClient(HttpClient http) : IReviewsClient
{
    private const int ReviewsPerProduct = 5;

    public async Task<IReadOnlyList<ReviewDto>> GetForProductsAsync(IReadOnlyCollection<long> productIds, CancellationToken ct)
    {
        var tasks = productIds.Select(id => GetOneAsync(id, ct)).ToList();
        var pages = await Task.WhenAll(tasks);
        return pages.SelectMany(p => p).ToList();
    }

    private async Task<IReadOnlyList<ReviewDto>> GetOneAsync(long productId, CancellationToken ct)
    {
        var page = await http.GetFromJsonAsync<ReviewsPage>(
            $"/api/reviews?productId={productId}&pageSize={ReviewsPerProduct}&sortBy=newest", ct);
        return page?.Items ?? [];
    }
}
