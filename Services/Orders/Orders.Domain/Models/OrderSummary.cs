namespace Orders.Domain.Models;

/// <summary>Рядок списку замовлень клієнта (результат usp_GetCustomerOrders).</summary>
public class OrderSummary
{
    public long OrderId { get; set; }
    public string Status { get; set; } = "";
    public DateTime OrderDate { get; set; }
    public int ItemsCount { get; set; }
    public decimal Total { get; set; }
    public int TotalRows { get; set; }
}

public class OrderStatusHistoryEntry
{
    public string? OldStatus { get; set; }
    public string NewStatus { get; set; } = "";
    public DateTime ChangedAt { get; set; }
    public string? ChangedBy { get; set; }
}
