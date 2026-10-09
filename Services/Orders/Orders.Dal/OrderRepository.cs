using Orders.Domain;

namespace Orders.Dal;

public class OrderRepository : BaseDapperRepository<Order>, IOrderRepository
{
    private const string SelectItemsByOrderSql =
        "SELECT Id, OrderId, ProductId, Quantity, UnitPrice FROM OrderItems WHERE OrderId = @OrderId";
    private const string SelectItemsByOrdersSql =
        "SELECT Id, OrderId, ProductId, Quantity, UnitPrice FROM OrderItems WHERE OrderId IN @Ids";
    private const string InsertItemSql =
        "INSERT INTO OrderItems (Id, OrderId, ProductId, Quantity, UnitPrice) VALUES (@Id, @OrderId, @ProductId, @Quantity, @UnitPrice)";

    public OrderRepository(DbSession session) : base(session) { }

    protected override string SelectByIdSql => "SELECT Id, CustomerId, OrderDate, TotalAmount, Status FROM Orders WHERE Id = @Id";
    protected override string SelectAllSql => "SELECT Id, CustomerId, OrderDate, TotalAmount, Status FROM Orders ORDER BY OrderDate DESC";
    protected override string InsertSql => "INSERT INTO Orders (Id, CustomerId, OrderDate, TotalAmount, Status) VALUES (@Id, @CustomerId, @OrderDate, @TotalAmount, @Status)";
    protected override string UpdateSql => "UPDATE Orders SET CustomerId = @CustomerId, OrderDate = @OrderDate, TotalAmount = @TotalAmount, Status = @Status WHERE Id = @Id";
    protected override string DeleteSql => "DELETE FROM Orders WHERE Id = @Id";

    protected override void EnsureId(Order entity)
    {
        if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();
    }

    public override async Task<Order?> GetByIdAsync(Guid id)
    {
        var order = await base.GetByIdAsync(id);
        if (order != null)
            order.Items = (await QueryAsync<OrderItem>(SelectItemsByOrderSql, new { OrderId = id })).ToList();
        return order;
    }

    public override async Task<IEnumerable<Order>> GetAllAsync()
    {
        var orders = (await base.GetAllAsync()).ToList();
        if (orders.Count == 0)
            return orders;

        var items = await QueryAsync<OrderItem>(SelectItemsByOrdersSql, new { Ids = orders.Select(o => o.Id).ToArray() });
        var lookup = items.ToLookup(i => i.OrderId);
        foreach (var order in orders)
            order.Items = lookup[order.Id].ToList();
        return orders;
    }

    public override async Task AddAsync(Order order)
    {
        await base.AddAsync(order);

        foreach (var item in order.Items)
        {
            item.OrderId = order.Id;
            item.Id = item.Id == Guid.Empty ? Guid.NewGuid() : item.Id;
        }

        if (order.Items.Count > 0)
            await ExecuteAsync(InsertItemSql, order.Items);
    }
}