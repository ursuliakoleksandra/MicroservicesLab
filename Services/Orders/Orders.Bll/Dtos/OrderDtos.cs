using System.ComponentModel.DataAnnotations;

namespace Orders.Bll.Dtos;

// ---------- вихідні DTO (лише дані, без логіки) ----------
public record OrderItemDto(long ProductId, string ProductName, decimal UnitPrice, decimal Discount, int Units, decimal LineTotal);

public record OrderDto(long Id, long CustomerId, string Status, DateTime OrderDate,
    string ShippingAddress, string? Comment, decimal Total, string RowVersion,
    IReadOnlyList<OrderItemDto> Items);

public record OrderSummaryDto(long OrderId, string Status, DateTime OrderDate, int ItemsCount, decimal Total);

public record OrderStatusHistoryDto(string? OldStatus, string NewStatus, DateTime ChangedAt, string? ChangedBy);

public record PagedResultDto<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

// ---------- вхідні DTO: базова валідація DataAnnotations (до виклику сервісу) ----------
public class CreateOrderItemDto
{
    [Range(1, long.MaxValue)] public long ProductId { get; set; }
    [Range(1, 1000)] public int Units { get; set; }
}

public class CreateOrderDto
{
    [Range(1, long.MaxValue)]
    public long CustomerId { get; set; }

    [Required, StringLength(400, MinimumLength = 3)]
    public string ShippingAddress { get; set; } = "";

    [StringLength(1000)]
    public string? Comment { get; set; }

    [Required, MinLength(1)]
    public List<CreateOrderItemDto> Items { get; set; } = new();
}

public class UpdateOrderDetailsDto
{
    [Required, StringLength(400, MinimumLength = 3)]
    public string ShippingAddress { get; set; } = "";

    [StringLength(1000)]
    public string? Comment { get; set; }

    /// <summary>Значення RowVersion (base64), отримане з GET /api/orders/{id} — оптимістична конкурентність.</summary>
    [Required]
    public string RowVersion { get; set; } = "";
}

public class PayOrderDto
{
    [Required, RegularExpression("^(Card|Cash|Transfer)$", ErrorMessage = "PaymentMethod must be Card, Cash or Transfer")]
    public string PaymentMethod { get; set; } = "";
}
