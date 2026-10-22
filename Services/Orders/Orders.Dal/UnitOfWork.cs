using System.Data;
using Microsoft.Data.SqlClient;
using Orders.Dal.Interfaces;
using Orders.Dal.Repositories;

namespace Orders.Dal;

/// <summary>
/// Unit of Work: одне з'єднання на всю операцію, транзакція з потрібним рівнем ізоляції,
/// гарантований Commit або Rollback. Усі репозиторії отримують ТЕ САМЕ з'єднання і доступ до ПОТОЧНОЇ транзакції.
///
/// Рівень ізоляції за замовчуванням — ReadCommitted: без «брудних» читань, мінімум блокувань.
/// Serializable/RepeatableRead дають менше аномалій читання, але більше блокувань і ризик deadlock.
/// Консистентність критичних місць забезпечують UPDLOCK у процедурах та умова Stock >= @Quantity.
/// Таймаути: підключення — Connect Timeout у рядку підключення (за замовчуванням 15 с);
/// команди — 30 с (RepositoryBase.CommandTimeoutSeconds).
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly SqlConnection _connection;
    private SqlTransaction? _transaction;

    public UnitOfWork(string connectionString)
    {
        _connection = new SqlConnection(connectionString);

        // усі репозиторії отримують ОДНЕ з'єднання і функцію доступу до ПОТОЧНОЇ транзакції
        Customers = new CustomerRepository(_connection, () => _transaction);
        Orders = new OrderRepository(_connection, () => _transaction);
        Products = new ProductRepository(_connection, () => _transaction);
    }

    public ICustomerRepository Customers { get; }
    public IOrderRepository Orders { get; }
    public IProductRepository Products { get; }

    public async Task BeginTransactionAsync(IsolationLevel level = IsolationLevel.ReadCommitted, CancellationToken ct = default)
    {
        if (_transaction is not null)
            throw new InvalidOperationException("A transaction is already in progress.");
        if (_connection.State != ConnectionState.Open)
            await _connection.OpenAsync(ct);
        _transaction = (SqlTransaction)await _connection.BeginTransactionAsync(level, ct);
    }

    public async Task CommitAsync(CancellationToken ct = default)
    {
        if (_transaction is null)
            throw new InvalidOperationException("No active transaction to commit.");
        var transaction = _transaction;
        try { await transaction.CommitAsync(ct); }
        finally
        {
            _transaction = null;
            await transaction.DisposeAsync();
        }
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
        if (_transaction is null) return;
        var transaction = _transaction;
        _transaction = null;
        try
        {
            // токен свідомо не використовується: відкат потрібно завершити, навіть якщо запит скасовано
            await transaction.RollbackAsync(CancellationToken.None);
        }
        catch (Exception ex) when (ex is InvalidOperationException or SqlException)
        {
            // сервер уже відкотив транзакцію сам (THROW + XACT_ABORT у процедурі) — це не помилка
        }
        finally
        {
            await transaction.DisposeAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null) await _transaction.DisposeAsync();   // незафіксована транзакція відкочується
        await _connection.DisposeAsync();
    }
}
