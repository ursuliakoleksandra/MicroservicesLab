namespace Orders.Dal.Interfaces;

/// <summary>Узагальнений CRUD (спільний для Dapper-репозиторіїв). Методи повертають моделі, а не DataReader/DataTable.</summary>
public interface IGenericRepository<T> where T : class
{
    Task<T?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);
    Task<long> AddAsync(T entity, CancellationToken ct = default);
    Task<bool> DeleteAsync(long id, CancellationToken ct = default);
}
