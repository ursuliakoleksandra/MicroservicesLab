namespace Orders.Dal;

public interface IUnitOfWork : IAsyncDisposable
{
    IOrderRepository Orders { get; }
    ICustomerRepository Customers { get; }
    IProductRepository Products { get; }
    Task BeginTransactionAsync();
    Task CommitAsync();
    Task RollbackAsync();
}

public class UnitOfWork : IUnitOfWork
{
    private readonly DbSession _session;

    public IOrderRepository Orders { get; }
    public ICustomerRepository Customers { get; }
    public IProductRepository Products { get; }

    public UnitOfWork(string connectionString)
    {
        _session = new DbSession(connectionString);
        Customers = new CustomerRepository(_session);
        Products = new ProductRepository(_session);
        Orders = new OrderRepository(_session);
    }

    public Task BeginTransactionAsync() => _session.BeginAsync();
    public Task CommitAsync() => _session.CommitAsync();
    public Task RollbackAsync() => _session.RollbackAsync();
    public ValueTask DisposeAsync() => _session.DisposeAsync();
}