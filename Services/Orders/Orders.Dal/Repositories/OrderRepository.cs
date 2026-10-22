using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Orders.Dal.Interfaces;
using Orders.Domain.Models;

namespace Orders.Dal.Repositories;

/// <summary>Dapper-репозиторій: CRUD — з BaseDapperRepository, специфічне — multi-mapping і збережені процедури Лаб. 01.</summary>
public sealed class OrderRepository : BaseDapperRepository<Order>, IOrderRepository
{
    private const string Actor = "api";

    public OrderRepository(SqlConnection connection, Func<SqlTransaction?> currentTransaction)
        : base(connection, currentTransaction, "dbo.Orders", identityKey: true,
               dbGeneratedColumns: [nameof(Order.OrderDate), nameof(Order.CreatedAt), nameof(Order.RowVersion)])
    { }

    protected override string ActiveFilter => "IsDeleted = 0";   // м'яке видалення: «видалені» замовлення не читаємо

    public async Task<Order?> GetWithItemsAsync(long orderId, CancellationToken ct = default)
    {
        const string sql = @"
            SELECT o.Id, o.CustomerId, o.Status, o.OrderDate, o.CreatedAt, o.CreatedBy,
                   o.UpdatedAt, o.UpdatedBy, o.IsDeleted, o.RowVersion,
                   d.ShippingAddress, d.Comment,
                   i.OrderId, i.ProductId, i.ProductName, i.UnitPrice, i.Discount, i.Units
            FROM dbo.Orders o
            JOIN dbo.OrderDetails d ON d.OrderId = o.Id
            LEFT JOIN dbo.OrderItems i ON i.OrderId = o.Id
            WHERE o.Id = @OrderId AND o.IsDeleted = 0
            ORDER BY i.ProductId";

        await EnsureOpenAsync(ct);
        Order? result = null;
        await Connection.QueryAsync<Order, OrderItem?, Order>(
            Command(sql, new { OrderId = orderId }, ct: ct),
            (order, item) =>                       // кожен рядок JOIN: Order + одна позиція
            {
                result ??= order;
                if (item is not null) result.Items.Add(item);
                return result;
            },
            splitOn: "OrderId");                   // з цього стовпця починається OrderItem
        return result;
    }

    public async Task<IReadOnlyList<OrderSummary>> GetByCustomerAsync(
        long customerId, string? status, int page, int pageSize, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        var rows = await Translated(() => Connection.QueryAsync<OrderSummary>(Command(
            "dbo.usp_GetCustomerOrders",
            new { CustomerId = customerId, Status = status, PageNumber = page, PageSize = pageSize },
            CommandType.StoredProcedure, ct)));
        return rows.AsList();
    }

    public async Task<IReadOnlyList<OrderStatusHistoryEntry>> GetHistoryAsync(long orderId, CancellationToken ct = default)
    {
        const string sql = @"SELECT OldStatus, NewStatus, ChangedAt, ChangedBy
                             FROM dbo.OrderStatusHistory WHERE OrderId = @OrderId ORDER BY ChangedAt, Id";
        await EnsureOpenAsync(ct);
        return (await Connection.QueryAsync<OrderStatusHistoryEntry>(Command(sql, new { OrderId = orderId }, ct: ct))).AsList();
    }

    public async Task AddDetailsAsync(long orderId, string shippingAddress, string? comment, CancellationToken ct = default)
    {
        const string sql = @"INSERT INTO dbo.OrderDetails (OrderId, ShippingAddress, Comment)
                             VALUES (@OrderId, @ShippingAddress, @Comment)";
        await EnsureOpenAsync(ct);
        await Translated(() => Connection.ExecuteAsync(
            Command(sql, new { OrderId = orderId, ShippingAddress = shippingAddress, Comment = comment }, ct: ct)));
    }

    public async Task AddItemsAsync(IEnumerable<OrderItem> items, CancellationToken ct = default)
    {
        const string sql = @"INSERT INTO dbo.OrderItems (OrderId, ProductId, ProductName, UnitPrice, Discount, Units)
                             VALUES (@OrderId, @ProductId, @ProductName, @UnitPrice, @Discount, @Units)";
        var rows = items.Select(i => new { i.OrderId, i.ProductId, i.ProductName, i.UnitPrice, i.Discount, i.Units }).ToList();
        await EnsureOpenAsync(ct);
        await Translated(() => Connection.ExecuteAsync(Command(sql, rows, ct: ct)));   // по одному виконанню на елемент списку
    }

    public async Task AddStatusHistoryAsync(long orderId, string? oldStatus, string newStatus, string? changedBy, CancellationToken ct = default)
    {
        const string sql = @"INSERT INTO dbo.OrderStatusHistory (OrderId, OldStatus, NewStatus, ChangedBy)
                             VALUES (@OrderId, @OldStatus, @NewStatus, @ChangedBy)";
        await EnsureOpenAsync(ct);
        await Translated(() => Connection.ExecuteAsync(
            Command(sql, new { OrderId = orderId, OldStatus = oldStatus, NewStatus = newStatus, ChangedBy = changedBy }, ct: ct)));
    }

    public async Task ChangeStatusAsync(long orderId, string newStatus, string? changedBy,
        string? paymentMethod = null, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        await Translated(() => Connection.ExecuteAsync(Command(
            "dbo.usp_ChangeOrderStatus",                               // THROW 50001/50002/... -> доменні винятки
            new { OrderId = orderId, NewStatus = newStatus, ChangedBy = changedBy ?? Actor, PaymentMethod = paymentMethod },
            CommandType.StoredProcedure, ct)));
    }

    public async Task UpdateDetailsAsync(long orderId, string shippingAddress, string? comment,
        string? updatedBy, byte[] rowVersion, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        await Translated(() => Connection.ExecuteAsync(Command(
            "dbo.usp_UpdateOrderDetails",
            new { OrderId = orderId, ShippingAddress = shippingAddress, Comment = comment,
                  UpdatedBy = updatedBy ?? Actor, RowVersion = rowVersion },
            CommandType.StoredProcedure, ct)));
    }

    /// <summary>М'яке видалення через usp_SoftDeleteOrder (дозволено лише для Submitted/Cancelled).</summary>
    public override async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        await EnsureOpenAsync(ct);
        await TranslatedVoid(() => Connection.ExecuteAsync(Command(
            "dbo.usp_SoftDeleteOrder", new { OrderId = id, DeletedBy = Actor },
            CommandType.StoredProcedure, ct)));
        return true;
    }
}
