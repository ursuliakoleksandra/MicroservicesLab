using Catalog.Contracts;
using Grpc.Core;
using Orders.Contracts;
using Platform.Aggregator.Models;
using Platform.Aggregator.Resilience;
using Reviews.Contracts;
using CatalogMoney = Catalog.Contracts.Money;
using OrdersMoney = Orders.Contracts.Money;

namespace Platform.Aggregator.Clients;

// gRPC-реалізації тих самих інтерфейсів, що й HTTP-клієнти (Лаб. 05): логіка агрегації не змінюється.
// Типізовані клієнти (CatalogService.CatalogServiceClient тощо) згенеровано з .proto; адреса — за логічним іменем ресурсу.
// Недоступність сервісу (Unavailable, DeadlineExceeded) — RpcException; перехоплює її Aggregator і додає попередження.

/// <summary>Дедлайн gRPC-виклику береться з профілю сервісу: критичні — довший, некритичні (Відгуки) — короткий.</summary>
internal static class GrpcDefaults
{
    public static DateTime Deadline(ServiceResilienceProfile profile) => DateTime.UtcNow + profile.GrpcDeadline;
}

public sealed class GrpcOrdersClient(OrdersService.OrdersServiceClient client) : IOrdersClient
{
    public async Task<OrderDto?> GetOrderAsync(long id, CancellationToken ct)
    {
        try
        {
            var r = await client.GetOrderAsync(new GetOrderRequest { Id = id }, deadline: GrpcDefaults.Deadline(ServiceProfiles.Orders), cancellationToken: ct);
            return new OrderDto(r.Id, r.CustomerId, r.Status, r.OrderDate.ToDateTime(), r.ShippingAddress,
                r.HasComment ? r.Comment : null, r.Total.ToDecimal(),
                r.Items.Select(i => new OrderItemDto(i.ProductId, i.ProductName, i.UnitPrice.ToDecimal(),
                    i.Discount.ToDecimal(), i.Units, i.LineTotal.ToDecimal())).ToList());
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            return null;                                       // як 404 у HTTP-клієнті
        }
    }
}

public sealed class GrpcCatalogClient(CatalogService.CatalogServiceClient client) : ICatalogClient
{
    public async Task<IReadOnlyList<ProductDto>> GetProductsAsync(IReadOnlyCollection<long> ids, CancellationToken ct)
    {
        var request = new GetProductsByIdsRequest { Ids = { ids } };
        var reply = await client.GetProductsByIdsAsync(request, deadline: GrpcDefaults.Deadline(ServiceProfiles.Catalog), cancellationToken: ct);   // один виклик на всі товари
        return reply.Products
            .Select(p => new ProductDto(p.Id, p.Name, p.Sku, p.Price.ToDecimal(), p.AvailableStock, p.IsActive))
            .ToList();
    }
}

public sealed class GrpcReviewsClient(ReviewsService.ReviewsServiceClient client) : IReviewsClient
{
    public async Task<IReadOnlyList<ReviewDto>> GetForProductsAsync(IReadOnlyCollection<long> productIds, CancellationToken ct)
    {
        var request = new GetForProductsRequest { ProductIds = { productIds }, PerProduct = 5 };
        var reply = await client.GetForProductsAsync(request, deadline: GrpcDefaults.Deadline(ServiceProfiles.Reviews), cancellationToken: ct);
        return reply.Reviews
            .Select(r => new ReviewDto(r.Id, r.ProductId, r.AuthorName, r.Rating, r.HasText ? r.Text : null, r.CreatedAt.ToDateTime()))
            .ToList();
    }
}

internal static class MoneyExtensions
{
    private const decimal Nano = 1_000_000_000m;
    public static decimal ToDecimal(this CatalogMoney? m) => m is null ? 0m : m.Units + m.Nanos / Nano;
    public static decimal ToDecimal(this OrdersMoney? m) => m is null ? 0m : m.Units + m.Nanos / Nano;
}
