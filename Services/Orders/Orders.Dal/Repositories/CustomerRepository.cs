using System.Data;
using Microsoft.Data.SqlClient;
using Orders.Dal.Interfaces;
using Orders.Domain.Models;

namespace Orders.Dal.Repositories;

/// <summary>
/// ЧИСТИЙ ADO.NET: SqlCommand + параметри + ручне читання через SqlDataReader.
/// Не успадковує IGenericRepository / BaseDapperRepository.
/// </summary>
public sealed class CustomerRepository : RepositoryBase, ICustomerRepository
{
    private const string SelectColumns = "Id, Email, FullName, Phone, CreatedAt";

    public CustomerRepository(SqlConnection connection, Func<SqlTransaction?> currentTransaction)
        : base(connection, currentTransaction) { }

    public async Task<Customer?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        const string sql = $"SELECT {SelectColumns} FROM dbo.Customers WHERE Id = @Id AND IsDeleted = 0";
        await EnsureOpenAsync(ct);
        await using var command = CreateCommand(sql);
        command.Parameters.Add("@Id", SqlDbType.BigInt).Value = id;               // лише параметр
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Map(reader) : null;
    }

    public async Task<Customer?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        const string sql = $"SELECT {SelectColumns} FROM dbo.Customers WHERE Email = @Email AND IsDeleted = 0";
        await EnsureOpenAsync(ct);
        await using var command = CreateCommand(sql);
        command.Parameters.Add("@Email", SqlDbType.NVarChar, 256).Value = email;
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Map(reader) : null;
    }

    public async Task<IReadOnlyList<Customer>> GetPagedAsync(int page, int pageSize, CancellationToken ct = default)
    {
        const string sql = $@"SELECT {SelectColumns} FROM dbo.Customers WHERE IsDeleted = 0
                              ORDER BY Id OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY";
        await EnsureOpenAsync(ct);
        await using var command = CreateCommand(sql);
        command.Parameters.Add("@Skip", SqlDbType.Int).Value = (page - 1) * pageSize;
        command.Parameters.Add("@Take", SqlDbType.Int).Value = pageSize;
        await using var reader = await command.ExecuteReaderAsync(ct);

        var result = new List<Customer>();
        while (await reader.ReadAsync(ct)) result.Add(Map(reader));
        return result;
    }

    public Task<long> AddAsync(Customer customer, string? createdBy, CancellationToken ct = default) =>
        Translated(async () =>
        {
            const string sql = @"INSERT INTO dbo.Customers (Email, FullName, Phone, CreatedBy)
                                 OUTPUT INSERTED.Id VALUES (@Email, @FullName, @Phone, @CreatedBy)";
            await EnsureOpenAsync(ct);
            await using var command = CreateCommand(sql);
            command.Parameters.Add("@Email", SqlDbType.NVarChar, 256).Value = customer.Email;
            command.Parameters.Add("@FullName", SqlDbType.NVarChar, 200).Value = customer.FullName;
            command.Parameters.Add("@Phone", SqlDbType.NVarChar, 30).Value = (object?)customer.Phone ?? DBNull.Value;
            command.Parameters.Add("@CreatedBy", SqlDbType.NVarChar, 100).Value = (object?)createdBy ?? DBNull.Value;
            return (long)(await command.ExecuteScalarAsync(ct))!;
        });

    public Task<bool> UpdateAsync(Customer customer, string? updatedBy, CancellationToken ct = default) =>
        Translated(async () =>
        {
            const string sql = @"UPDATE dbo.Customers
                                 SET Email = @Email, FullName = @FullName, Phone = @Phone,
                                     UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UpdatedBy
                                 WHERE Id = @Id AND IsDeleted = 0";
            await EnsureOpenAsync(ct);
            await using var command = CreateCommand(sql);
            command.Parameters.Add("@Id", SqlDbType.BigInt).Value = customer.Id;
            command.Parameters.Add("@Email", SqlDbType.NVarChar, 256).Value = customer.Email;
            command.Parameters.Add("@FullName", SqlDbType.NVarChar, 200).Value = customer.FullName;
            command.Parameters.Add("@Phone", SqlDbType.NVarChar, 30).Value = (object?)customer.Phone ?? DBNull.Value;
            command.Parameters.Add("@UpdatedBy", SqlDbType.NVarChar, 100).Value = (object?)updatedBy ?? DBNull.Value;
            return await command.ExecuteNonQueryAsync(ct) > 0;
        });

    public async Task<bool> SoftDeleteAsync(long id, string? deletedBy, CancellationToken ct = default)
    {
        const string sql = @"UPDATE dbo.Customers
                             SET IsDeleted = 1, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @DeletedBy
                             WHERE Id = @Id AND IsDeleted = 0";
        await EnsureOpenAsync(ct);
        await using var command = CreateCommand(sql);
        command.Parameters.Add("@Id", SqlDbType.BigInt).Value = id;
        command.Parameters.Add("@DeletedBy", SqlDbType.NVarChar, 100).Value = (object?)deletedBy ?? DBNull.Value;
        return await command.ExecuteNonQueryAsync(ct) > 0;
    }

    // команда завжди прив'язана до ПОТОЧНОЇ транзакції Unit of Work (або null)
    private SqlCommand CreateCommand(string sql) =>
        new(sql, Connection, Transaction) { CommandTimeout = CommandTimeoutSeconds };

    private static Customer Map(SqlDataReader r) => new()
    {
        Id = r.GetInt64(0),
        Email = r.GetString(1),
        FullName = r.GetString(2),
        Phone = r.IsDBNull(3) ? null : r.GetString(3),
        CreatedAt = r.GetDateTime(4)
    };
}
