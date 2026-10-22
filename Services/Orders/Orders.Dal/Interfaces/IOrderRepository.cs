using Orders.Domain.Models;

namespace Orders.Dal.Interfaces;

public interface IOrderRepository : IGenericRepository<Order>
{
    /// <summary>Замовлення + деталі + позиції одним запитом (Dapper multi-mapping).</summary>
    Task<Order?> GetWithItemsAsync(long orderId, CancellationToken ct = default);

    /// <summary>Список замовлень клієнта з пагінацією (процедура usp_GetCustomerOrders).</summary>
    Task<IReadOnlyList<OrderSummary>> GetByCustomerAsync(long customerId, string? status, int page, int pageSize, CancellationToken ct = default);

    Task<IReadOnlyList<OrderStatusHistoryEntry>> GetHistoryAsync(long orderId, CancellationToken ct = default);

    Task AddDetailsAsync(long orderId, string shippingAddress, string? comment, CancellationToken ct = default);
    Task AddItemsAsync(IEnumerable<OrderItem> items, CancellationToken ct = default);
    Task AddStatusHistoryAsync(long orderId, string? oldStatus, string newStatus, string? changedBy, CancellationToken ct = default);

    /// <summary>Транзакційна зміна статусу (usp_ChangeOrderStatus). Недопустимий перехід -> BusinessConflictException.</summary>
    Task ChangeStatusAsync(long orderId, string newStatus, string? changedBy, string? paymentMethod = null, CancellationToken ct = default);

    /// <summary>Зміна адреси/коментаря з оптимістичною конкурентністю (usp_UpdateOrderDetails).</summary>
    Task UpdateDetailsAsync(long orderId, string shippingAddress, string? comment, string? updatedBy, byte[] rowVersion, CancellationToken ct = default);
}
