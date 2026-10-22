using Catalog.Dal.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Dal.Repositories;

/// <summary>Async Generic Repository на DbSet&lt;T&gt;. Зміни в БД не зберігає — це робить Unit of Work.</summary>
public class GenericRepository<T> : IGenericRepository<T> where T : class
{
    protected readonly CatalogDbContext Context;
    protected readonly DbSet<T> Set;

    public GenericRepository(CatalogDbContext context)
    {
        Context = context;
        Set = context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(long id, CancellationToken ct = default)
        => await Set.FindAsync(new object[] { id }, ct);

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
        => await Set.AsNoTracking().ToListAsync(ct);

    public virtual async Task AddAsync(T entity, CancellationToken ct = default)
        => await Set.AddAsync(entity, ct);

    // Update/Delete лише змінюють стан сутності в контексті, тому синхронні
    public virtual void Update(T entity) => Set.Update(entity);
    public virtual void Delete(T entity) => Set.Remove(entity);
}
