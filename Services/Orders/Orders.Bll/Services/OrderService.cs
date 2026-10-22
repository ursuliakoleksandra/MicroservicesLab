using System.Data;
using AutoMapper;
using Microsoft.Extensions.Logging;
using Orders.Bll.Dtos;
using Orders.Dal.Interfaces;
using Orders.Domain.Exceptions;
using Orders.Domain.Models;

namespace Orders.Bll.Services;

/// <summary>
/// Єдиний власник бізнес-логіки замовлень. SQL тут немає: лише Unit of Work і репозиторії.
/// Транзакційні операції (Create, Confirm, Cancel) змінюють кілька таблиць в одній транзакції з відкатом при помилці.
/// </summary>
public sealed class OrderService(IUnitOfWork uow, IMapper mapper, ILogger<OrderService> logger) : IOrderService
{
    private const string Actor = "api";

    public async Task<OrderDto> GetByIdAsync(long id, CancellationToken ct)
    {
        var order = await uow.Orders.GetWithItemsAsync(id, ct)
                    ?? throw new NotFoundException($"Order {id} not found");
        return mapper.Map<OrderDto>(order);
    }

    public async Task<PagedResultDto<OrderSummaryDto>> GetCustomerOrdersAsync(
        long customerId, string? status, int page, int pageSize, CancellationToken ct)
    {
        // бізнес-валідація (потребує знань предметної області / стану БД)
        if (page < 1) throw new ValidationException("Page must be >= 1");
        if (pageSize is < 1 or > 100) throw new ValidationException("PageSize must be between 1 and 100");
        if (status is not null && !OrderStatus.All.Contains(status))
            throw new ValidationException($"Unknown status '{status}'. Allowed: {string.Join(", ", OrderStatus.All)}");
        if (await uow.Customers.GetByIdAsync(customerId, ct) is null)
            throw new NotFoundException($"Customer {customerId} not found");

        var rows = await uow.Orders.GetByCustomerAsync(customerId, status, page, pageSize, ct);
        var total = rows.Count > 0 ? rows[0].TotalRows : 0;
        return new PagedResultDto<OrderSummaryDto>(mapper.Map<List<OrderSummaryDto>>(rows), total, page, pageSize);
    }

    public async Task<IReadOnlyList<OrderStatusHistoryDto>> GetHistoryAsync(long id, CancellationToken ct)
    {
        if (await uow.Orders.GetByIdAsync(id, ct) is null)
            throw new NotFoundException($"Order {id} not found");
        return mapper.Map<List<OrderStatusHistoryDto>>(await uow.Orders.GetHistoryAsync(id, ct));
    }

    public async Task<OrderDto> CreateAsync(CreateOrderDto dto, CancellationToken ct)
    {
        if (dto.Items.GroupBy(i => i.ProductId).Any(g => g.Count() > 1))
            throw new ValidationException("Order contains the same product more than once; merge the units into one line");

        long orderId;
        await uow.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            var customer = await uow.Customers.GetByIdAsync(dto.CustomerId, ct)
                           ?? throw new ValidationException($"Customer {dto.CustomerId} does not exist");

            var products = (await uow.Products.GetByIdsAsync(dto.Items.Select(i => i.ProductId).ToList(), ct))
                           .ToDictionary(p => p.Id);

            var items = new List<OrderItem>();
            foreach (var line in dto.Items)
            {
                if (!products.TryGetValue(line.ProductId, out var product) || !product.IsActive)
                    throw new ValidationException($"Product {line.ProductId} does not exist or is not available");

                // ЗНІМОК назви й ціни на момент замовлення (політика дублювання з Лаб. 01)
                items.Add(new OrderItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    UnitPrice = product.Price,
                    Units = line.Units
                });
            }

            orderId = await uow.Orders.AddAsync(new Order { CustomerId = customer.Id, CreatedBy = Actor }, ct);
            await uow.Orders.AddDetailsAsync(orderId, dto.ShippingAddress.Trim(), dto.Comment, ct);
            foreach (var item in items) item.OrderId = orderId;
            await uow.Orders.AddItemsAsync(items, ct);
            await uow.Orders.AddStatusHistoryAsync(orderId, null, OrderStatus.Submitted, Actor, ct);

            await uow.CommitAsync(ct);
        }
        catch
        {
            await uow.RollbackAsync(ct);
            throw;
        }

        logger.LogInformation("Order {OrderId} created for customer {CustomerId}", orderId, dto.CustomerId);
        return await GetByIdAsync(orderId, ct);
    }

    public async Task<OrderDto> UpdateDetailsAsync(long id, UpdateOrderDetailsDto dto, CancellationToken ct)
    {
        byte[] rowVersion;
        try { rowVersion = Convert.FromBase64String(dto.RowVersion); }
        catch (FormatException) { throw new ValidationException("RowVersion must be the base64 value returned by GET /api/orders/{id}"); }
        if (rowVersion.Length != 8) throw new ValidationException("RowVersion has an invalid length");

        await uow.Orders.UpdateDetailsAsync(id, dto.ShippingAddress.Trim(), dto.Comment, Actor, rowVersion, ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct)
    {
        await uow.Orders.DeleteAsync(id, ct);   // м'яке видалення; процедура відхиляє недозволені статуси
        logger.LogInformation("Order {OrderId} soft-deleted", id);
    }

    public async Task ConfirmOrderAsync(long id, CancellationToken ct)
    {
        await uow.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            var order = await uow.Orders.GetWithItemsAsync(id, ct)
                        ?? throw new NotFoundException($"Order {id} not found");
            if (order.Items.Count == 0)
                throw new BusinessConflictException($"Order {id} has no items");

            // Submitted -> AwaitingValidation -> StockConfirmed; допустимість кожного переходу перевіряє процедура (THROW 50002)
            if (order.Status == OrderStatus.Submitted)
                await uow.Orders.ChangeStatusAsync(id, OrderStatus.AwaitingValidation, Actor, null, ct);
            await uow.Orders.ChangeStatusAsync(id, OrderStatus.StockConfirmed, Actor, null, ct);

            // фіксований порядок блокувань (за ProductId) зменшує ризик deadlock між паралельними підтвердженнями;
            // брак товару -> BusinessConflictException -> відкат і статусу
            foreach (var item in order.Items.OrderBy(i => i.ProductId))
                await uow.Products.DecreaseStockAsync(item.ProductId, item.Units, ct);

            await uow.CommitAsync(ct);
        }
        catch
        {
            await uow.RollbackAsync(ct);   // статус і залишки повертаються назад
            throw;
        }
        logger.LogInformation("Order {OrderId} confirmed", id);
    }

    public async Task PayOrderAsync(long id, PayOrderDto dto, CancellationToken ct)
    {
        if (dto.PaymentMethod is not (PaymentMethods.Card or PaymentMethods.Cash or PaymentMethods.Transfer))
            throw new ValidationException("PaymentMethod must be Card, Cash or Transfer");

        // процедура сама атомарно створює PaymentRecord і змінює статус (власна транзакція)
        await uow.Orders.ChangeStatusAsync(id, OrderStatus.Paid, Actor, dto.PaymentMethod, ct);
        logger.LogInformation("Order {OrderId} paid", id);
    }

    public async Task ShipOrderAsync(long id, CancellationToken ct)
    {
        await uow.Orders.ChangeStatusAsync(id, OrderStatus.Shipped, Actor, null, ct);   // неоплачене -> 409
        logger.LogInformation("Order {OrderId} shipped", id);
    }

    public async Task CancelOrderAsync(long id, CancellationToken ct)
    {
        await uow.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            var order = await uow.Orders.GetWithItemsAsync(id, ct)
                        ?? throw new NotFoundException($"Order {id} not found");
            var stockWasReserved = order.Status == OrderStatus.StockConfirmed;

            await uow.Orders.ChangeStatusAsync(id, OrderStatus.Cancelled, Actor, null, ct);   // після оплати -> 409
            if (stockWasReserved)
                foreach (var item in order.Items.OrderBy(i => i.ProductId))
                    await uow.Products.IncreaseStockAsync(item.ProductId, item.Units, ct);

            await uow.CommitAsync(ct);
        }
        catch
        {
            await uow.RollbackAsync(ct);
            throw;
        }
        logger.LogInformation("Order {OrderId} cancelled", id);
    }
}
