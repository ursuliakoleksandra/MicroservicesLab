using Microsoft.Data.SqlClient;
using System.Data;

namespace Orders.Dal;

public interface IUnitOfWork : IAsyncDisposable
{
    IOrderRepository Orders { get; }
    ICustomerRepository Customers { get; }
    IProductRepository Products { get; }
    Task BeginTransactionAsync();
    Task CommitAsync();
    Task RollbackAsync();
    IDbTransaction? Transaction { get; }
}

public class UnitOfWork : IUnitOfWork
{
    private readonly SqlConnection _connection;
    private SqlTransaction? _transaction;

    public IOrderRepository Orders { get; }
    public ICustomerRepository Customers { get; }
    public IProductRepository Products { get; }

    public IDbTransaction? Transaction => _transaction;

    public UnitOfWork(string connectionString)
    {
        _connection = new SqlConnection(connectionString);
        Customers = new CustomerRepository(connectionString);
        Products = new ProductRepository(connectionString);
        Orders = new OrderRepository(connectionString);
    }

    public async Task BeginTransactionAsync()
    {
        if (_connection.State != ConnectionState.Open)
            await _connection.OpenAsync();

        _transaction = _connection.BeginTransaction();
    }

    public async Task CommitAsync()
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackAsync()
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction != null)
            await _transaction.DisposeAsync();

        await _connection.DisposeAsync();
    }
}