namespace Catalog.Dal.Interfaces;

/// <summary>
/// Async Generic Repository на DbSet&lt;T&gt;. Update/Delete лише змінюють стан сутності в контексті;
/// збереження в БД виконує Unit of Work одним SaveChangesAsync().
/// </summary>
public interface IGenericRepository<T> where T : class
{
    Task<T?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Delete(T entity);
}
