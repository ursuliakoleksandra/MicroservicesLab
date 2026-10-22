using System.ComponentModel.DataAnnotations.Schema;

namespace Orders.Domain.Models;

/// <summary>Агрегат «Замовлення»: Order + OrderDetails (1:1) + OrderItem (1:N).</summary>
public class Order
{
    public long Id { get; set; }
    public long CustomerId { get; set; }
    public string Status { get; set; } = OrderStatus.Submitted;
    public DateTime OrderDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    // дані з таблиць OrderDetails (1:1) і OrderItems (1:N) — не є стовпцями таблиці Orders
    [NotMapped] public string ShippingAddress { get; set; } = "";
    [NotMapped] public string? Comment { get; set; }
    [NotMapped] public List<OrderItem> Items { get; set; } = new();

    public decimal Total => Items.Sum(i => i.LineTotal);
}
