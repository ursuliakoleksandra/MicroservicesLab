using Dapper;
using Microsoft.Data.SqlClient;
using Orders.Domain;
using System.Data;

namespace Orders.Dal;

public class ProductRepository : IProductRepository
{
    private readonly string _connectionString;

    public ProductRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        await using var connection = new SqlConnection(_connectionString);
        const string sql = "SELECT COUNT(1) FROM Products WHERE Id = @Id";
        var count = await connection.ExecuteScalarAsync<int>(sql, new { Id = id });
        return count > 0;
    }

    public async Task<decimal> GetPriceAsync(Guid id)
    {
        await using var connection = new SqlConnection(_connectionString);
        const string sql = "SELECT Price FROM Products WHERE Id = @Id";
        return await connection.ExecuteScalarAsync<decimal>(sql, new { Id = id });
    }
}

public class OrderRepository : IOrderRepository
{
    private readonly string _connectionString;

    public OrderRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<Order?> GetByIdAsync(Guid id)
    {
        await using var connection = new SqlConnection(_connectionString);
        const string sqlOrder = "SELECT * FROM Orders WHERE Id = @Id";
        const string sqlItems = "SELECT * FROM OrderItems WHERE OrderId = @OrderId";

        var order = await connection.QueryFirstOrDefaultAsync<Order>(sqlOrder, new { Id = id });
        if (order != null)
        {
            var items = await connection.QueryAsync<OrderItem>(sqlItems, new { OrderId = id });
            order.Items = items.ToList();
        }

        return order;
    }

    public async Task CreateAsync(Order order, IDbTransaction transaction)
    {
        const string sqlOrder = @"
            INSERT INTO Orders (Id, CustomerId, OrderDate, TotalAmount, Status)
            VALUES (@Id, @CustomerId, @OrderDate, @TotalAmount, @Status)";

        const string sqlItem = @"
            INSERT INTO OrderItems (Id, OrderId, ProductId, Quantity, UnitPrice)
            VALUES (@Id, @OrderId, @ProductId, @Quantity, @UnitPrice)";

        await transaction.Connection.ExecuteAsync(sqlOrder, order, transaction);

        foreach (var item in order.Items)
        {
            item.OrderId = order.Id;
            item.Id = item.Id == Guid.Empty ? Guid.NewGuid() : item.Id;
            await transaction.Connection.ExecuteAsync(sqlItem, item, transaction);
        }
    }
}