using System.Data;
using Microsoft.Data.SqlClient;

namespace Orders.Dal.Repositories;

/// <summary>
/// Спільна основа репозиторіїв: отримує З'ЄДНАННЯ та доступ до ПОТОЧНОЇ транзакції Unit of Work
/// (а не рядок підключення), тому всі репозиторії однієї бізнес-операції працюють в одній транзакції.
/// </summary>
public abstract class RepositoryBase
{
    /// <summary>Таймаут команд (секунди). Довші запити краще перервати, ніж тримати блокування.</summary>
    protected const int CommandTimeoutSeconds = 30;

    protected readonly SqlConnection Connection;
    private readonly Func<SqlTransaction?> _currentTransaction;

    protected RepositoryBase(SqlConnection connection, Func<SqlTransaction?> currentTransaction)
    {
        Connection = connection;
        _currentTransaction = currentTransaction;
    }

    /// <summary>Поточна транзакція Unit of Work (або null, якщо її не розпочато).</summary>
    protected SqlTransaction? Transaction => _currentTransaction();

    protected async Task EnsureOpenAsync(CancellationToken ct)
    {
        if (Connection.State != ConnectionState.Open)
            await Connection.OpenAsync(ct);
    }

    /// <summary>Виконує дію й перетворює відомі SqlException на доменні винятки.</summary>
    protected static async Task<TResult> Translated<TResult>(Func<Task<TResult>> action)
    {
        try { return await action(); }
        catch (SqlException ex) when (SqlErrorTranslator.CanTranslate(ex))
        {
            throw SqlErrorTranslator.Translate(ex);
        }
    }

    protected static async Task TranslatedVoid(Func<Task> action)
    {
        try { await action(); }
        catch (SqlException ex) when (SqlErrorTranslator.CanTranslate(ex))
        {
            throw SqlErrorTranslator.Translate(ex);
        }
    }
}
