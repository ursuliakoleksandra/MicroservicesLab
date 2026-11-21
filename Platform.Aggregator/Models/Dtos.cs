namespace Platform.Aggregator.Models;

// ---------- DTO, які Aggregator читає з мікросервісів (лише потрібні поля; решта ігнорується) ----------
public record OrderItemDto(long ProductId, string ProductName, decimal UnitPrice, decimal Discount, int Units, decimal LineTotal);

public record OrderDto(long Id, long CustomerId, string Status, DateTime OrderDate, string ShippingAddress,
    string? Comment, decimal Total, IReadOnlyList<OrderItemDto> Items);

public record ProductDto(long Id, string Name, string Sku, decimal Price, int AvailableStock, bool IsActive);

public record ReviewDto(string Id, long ProductId, string AuthorName, int Rating, string? Text, DateTime CreatedAt);

/// <summary>Відповідь reviews-api: items + метадані пагінації.</summary>
public record ReviewsPage(IReadOnlyList<ReviewDto> Items, int TotalCount);

// ---------- композитна відповідь під бізнес-сценарій «повна картка замовлення» ----------
/// <summary>
/// Order — з orders-api, Products — актуальні дані з catalog-api (лише товари цього замовлення),
/// Reviews — відгуки на ці товари з reviews-api, Warnings — що не вдалося отримати.
/// </summary>
public record FullOrderDto(
    OrderDto Order,
    IReadOnlyList<ProductDto> Products,
    IReadOnlyList<ReviewDto> Reviews,
    IReadOnlyList<string> Warnings);
