using Dapper;
using Microsoft.Data.SqlClient;
using Orders.Dal.Interfaces;

namespace Orders.Dal.Repositories;

public sealed class ProcessedEventRepository(SqlConnection connection, Func<SqlTransaction?> currentTransaction)
    : RepositoryBase(connection, currentTransaction), IProcessedEventRepository
{
    private const int DuplicateKey = 2627;      // порушення PRIMARY KEY / UNIQUE constraint
    private const int DuplicateIndex = 2601;    // порушення унікального індексу

    public async Task<bool> TryAddAsync(Guid eventId, string eventType, CancellationToken ct = default)
    {
        const string sql = "INSERT INTO dbo.ProcessedEvents (EventId, EventType) VALUES (@EventId, @EventType)";
        await EnsureOpenAsync(ct);
        try
        {
            await Connection.ExecuteAsync(new CommandDefinition(sql, new { EventId = eventId, EventType = eventType },
                Transaction, commandTimeout: CommandTimeoutSeconds, cancellationToken: ct));
            return true;
        }
        catch (SqlException ex) when (ex.Number is DuplicateKey or DuplicateIndex)
        {
            return false;      // подію вже оброблено: це не помилка, а сигнал для ідемпотентного пропуску
        }
    }
}
