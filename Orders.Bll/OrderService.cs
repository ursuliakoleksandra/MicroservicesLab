using AutoMapper;
using Orders.Dal;
using Orders.Domain;

namespace Orders.Bll;

public interface IOrderService
{
    Task<OrderDto> CreateOrderAsync(CreateOrderDto dto);
    Task<OrderDto> GetOrderByIdAsync(Guid id);
}

public class OrderService : IOrderService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;

    public OrderService(IUnitOfWork uow, IMapper mapper)
    {
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<OrderDto> CreateOrderAsync(CreateOrderDto dto)
    {
        if (dto.Items == null || !dto.Items.Any())
            throw new BusinessValidationException("Замовлення повинно містити хоча б один товар.");

        // 1. Перевіряємо існування покупця
        var customer = await _uow.Customers.GetByIdAsync(dto.CustomerId);
        if (customer == null)
            throw new NotFoundException($"Покупця з ID '{dto.CustomerId}' не знайдено.");

        // 2. Мапимо DTO в Domain Model
        var order = _mapper.Map<Order>(dto);
        decimal totalAmount = 0;

        foreach (var item in order.Items)
        {
            // Перевіряємо наявність товару та його актуальну ціну
            var exists = await _uow.Products.ExistsAsync(item.ProductId);
            if (!exists)
                throw new NotFoundException($"Товар з ID '{item.ProductId}' не знайдено.");

            var price = await _uow.Products.GetPriceAsync(item.ProductId);
            item.UnitPrice = price;
            totalAmount += price * item.Quantity;
        }

        order.TotalAmount = totalAmount;

        // 3. Збереження замовлення у транзакціїчерез Unit of Work
        await _uow.BeginTransactionAsync();
        try
        {
            await _uow.Orders.CreateAsync(order, _uow.Transaction!);
            await _uow.CommitAsync();
        }
        catch
        {
            await _uow.RollbackAsync();
            throw;
        }

        return _mapper.Map<OrderDto>(order);
    }

    public async Task<OrderDto> GetOrderByIdAsync(Guid id)
    {
        var order = await _uow.Orders.GetByIdAsync(id);
        if (order == null)
            throw new NotFoundException($"Замовлення з ID '{id}' не знайдено.");

        return _mapper.Map<OrderDto>(order);
    }
}