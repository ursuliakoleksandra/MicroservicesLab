using System.Data;
using Microsoft.Data.SqlClient;

namespace Orders.Dal;

public sealed class DbSession : IAsyncDisposable
{
    private readonly SqlConnection _connection;

    public DbSession(string connectionString)
    {
        _connection = new SqlConnection(connectionString);
    }

    public SqlTransaction? Transaction { get; private set; }

    public async Task<SqlConnection> GetConnectionAsync()
    {
        if (_connection.State != ConnectionState.Open)
            await _connection.OpenAsync();
        return _connection;
    }

    public async Task BeginAsync()
    {
        if (Transaction != null)
            throw new InvalidOperationException("Транзакція вже розпочата.");

        var connection = await GetConnectionAsync();
        Transaction = (SqlTransaction)await connection.BeginTransactionAsync();
    }

    public async Task CommitAsync()
    {
        if (Transaction == null) return;
        await Transaction.CommitAsync();
        await Transaction.DisposeAsync();
        Transaction = null;
    }

    public async Task RollbackAsync()
    {
        if (Transaction == null) return;
        await Transaction.RollbackAsync();
        await Transaction.DisposeAsync();
        Transaction = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (Transaction != null)
            await Transaction.DisposeAsync();
        await _connection.DisposeAsync();
    }
}