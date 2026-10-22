using System.Data;

namespace Orders.Dal.Interfaces;

/// <summary>
/// Unit of Work: ОДНЕ з'єднання й ОДНА (поточна) транзакція на всі репозиторії.
/// Реєструється як Scoped — один екземпляр і одне з'єднання на HTTP-запит.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    ICustomerRepository Customers { get; }
    IOrderRepository Orders { get; }
    IProductRepository Products { get; }

    Task BeginTransactionAsync(IsolationLevel level = IsolationLevel.ReadCommitted, CancellationToken ct = default);
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}
