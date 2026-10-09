using AutoMapper;
using Orders.Dal;
using Orders.Domain;

namespace Orders.Bll;

public interface IOrderService
{
    Task<OrderDto> CreateOrderAsync(CreateOrderDto dto);
    Task<OrderDto> GetOrderByIdAsync(Guid id);
    Task<OrderDto> ConfirmOrderAsync(Guid id);
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

        if (dto.Items.Any(i => i.Quantity <= 0))
            throw new BusinessValidationException("Кількість кожного товару має бути більшою за 0.");

        if (dto.Items.GroupBy(i => i.ProductId).Any(g => g.Count() > 1))
            throw new BusinessValidationException("Один товар не можна додавати до замовлення кілька разів.");

        var customer = await _uow.Customers.GetByIdAsync(dto.CustomerId);
        if (customer == null)
            throw new NotFoundException($"Покупця з ID '{dto.CustomerId}' не знайдено.");

        var order = _mapper.Map<Order>(dto);
        decimal totalAmount = 0;

        foreach (var item in order.Items)
        {
            var product = await _uow.Products.GetByIdAsync(item.ProductId);
            if (product == null)
                throw new NotFoundException($"Товар з ID '{item.ProductId}' не знайдено.");

            item.UnitPrice = product.Price;
            totalAmount += product.Price * item.Quantity;
        }

        order.TotalAmount = totalAmount;

        await _uow.BeginTransactionAsync();
        try
        {
            // Репозиторій 1: створення замовлення
            await _uow.Orders.AddAsync(order);

            // Репозиторій 2: списання зі складу. Якщо товару не вистачає, відкочується і замовлення.
            foreach (var item in order.Items)
            {
                var decreased = await _uow.Products.TryDecreaseStockAsync(item.ProductId, item.Quantity);
                if (!decreased)
                    throw new BusinessConflictException(
                        $"Недостатньо товару з ID '{item.ProductId}' на складі.");
            }

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

    public async Task<OrderDto> ConfirmOrderAsync(Guid id)
    {
        var order = await _uow.Orders.GetByIdAsync(id);
        if (order == null)
            throw new NotFoundException($"Замовлення з ID '{id}' не знайдено.");

        if (order.Status != OrderStatus.New)
            throw new BusinessConflictException(
                $"Замовлення має статус '{order.Status}'. Підтвердити можна лише нове замовлення.");

        order.Status = OrderStatus.Confirmed;

        await _uow.BeginTransactionAsync();
        try
        {
            await _uow.Orders.UpdateAsync(order);
            await _uow.CommitAsync();
        }
        catch
        {
            await _uow.RollbackAsync();
            throw;
        }

        return _mapper.Map<OrderDto>(order);
    }
}