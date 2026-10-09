using Dapper;

namespace Orders.Dal;

/// <summary>Спільна база для Dapper-репозиторіїв: одне з'єднання й транзакція з DbSession.</summary>
public abstract class BaseDapperRepository<T> : IGenericRepository<T> where T : class
{
    private readonly DbSession _session;

    protected BaseDapperRepository(DbSession session)
    {
        _session = session;
    }

    protected abstract string SelectByIdSql { get; }
    protected abstract string SelectAllSql { get; }
    protected abstract string InsertSql { get; }
    protected abstract string UpdateSql { get; }
    protected abstract string DeleteSql { get; }

    protected abstract void EnsureId(T entity);

    protected async Task<IEnumerable<TResult>> QueryAsync<TResult>(string sql, object? parameters = null)
    {
        var connection = await _session.GetConnectionAsync();
        return await connection.QueryAsync<TResult>(sql, parameters, _session.Transaction);
    }

    protected async Task<TResult?> QueryFirstOrDefaultAsync<TResult>(string sql, object? parameters = null)
    {
        var connection = await _session.GetConnectionAsync();
        return await connection.QueryFirstOrDefaultAsync<TResult>(sql, parameters, _session.Transaction);
    }

    protected async Task<int> ExecuteAsync(string sql, object? parameters = null)
    {
        var connection = await _session.GetConnectionAsync();
        return await connection.ExecuteAsync(sql, parameters, _session.Transaction);
    }

    public virtual Task<T?> GetByIdAsync(Guid id) =>
        QueryFirstOrDefaultAsync<T>(SelectByIdSql, new { Id = id });

    public virtual Task<IEnumerable<T>> GetAllAsync() =>
        QueryAsync<T>(SelectAllSql);

    public virtual async Task AddAsync(T entity)
    {
        EnsureId(entity);
        await ExecuteAsync(InsertSql, entity);
    }

    public virtual async Task<bool> UpdateAsync(T entity) =>
        await ExecuteAsync(UpdateSql, entity) > 0;

    public virtual async Task<bool> DeleteAsync(Guid id) =>
        await ExecuteAsync(DeleteSql, new { Id = id }) > 0;
}