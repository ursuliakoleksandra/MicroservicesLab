using Orders.Bll.Dtos;

namespace Orders.Bll.Services;

public interface IOrderService
{
    Task<OrderDto> GetByIdAsync(long id, CancellationToken ct);
    Task<PagedResultDto<OrderSummaryDto>> GetCustomerOrdersAsync(long customerId, string? status, int page, int pageSize, CancellationToken ct);
    Task<IReadOnlyList<OrderStatusHistoryDto>> GetHistoryAsync(long id, CancellationToken ct);
    Task<OrderDto> CreateAsync(CreateOrderDto dto, CancellationToken ct);
    Task<OrderDto> UpdateDetailsAsync(long id, UpdateOrderDetailsDto dto, CancellationToken ct);
    Task DeleteAsync(long id, CancellationToken ct);

    /// <summary>Підтвердження: статус -> StockConfirmed + зменшення залишку товарів в ОДНІЙ транзакції.</summary>
    Task ConfirmOrderAsync(long id, CancellationToken ct);
    Task PayOrderAsync(long id, PayOrderDto dto, CancellationToken ct);
    Task ShipOrderAsync(long id, CancellationToken ct);

    /// <summary>Скасування: статус -> Cancelled (+ повернення залишку, якщо замовлення було підтверджене) в ОДНІЙ транзакції.</summary>
    Task CancelOrderAsync(long id, CancellationToken ct);
}

public interface ICustomerService
{
    Task<CustomerDto> GetByIdAsync(long id, CancellationToken ct);
    Task<PagedResultDto<CustomerDto>> GetPagedAsync(int page, int pageSize, CancellationToken ct);
    Task<CustomerDto> CreateAsync(SaveCustomerDto dto, CancellationToken ct);
    Task<CustomerDto> UpdateAsync(long id, SaveCustomerDto dto, CancellationToken ct);
    Task DeleteAsync(long id, CancellationToken ct);
}

public interface IProductService
{
    Task<IReadOnlyList<ProductDto>> GetAllAsync(CancellationToken ct);
    Task<ProductDto> GetByIdAsync(long id, CancellationToken ct);
    Task<ProductDto> SyncFromCatalogAsync(long id, SyncProductDto dto, CancellationToken ct);
}
