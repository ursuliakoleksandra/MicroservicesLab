using Microsoft.Data.SqlClient;
using Orders.Domain;

namespace Orders.Dal;

public class CustomerRepository : ICustomerRepository
{
    private readonly DbSession _session;

    public CustomerRepository(DbSession session)
    {
        _session = session;
    }

    private async Task<SqlCommand> CreateCommandAsync(string sql)
    {
        var connection = await _session.GetConnectionAsync();
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = _session.Transaction;
        return command;
    }

    private static Customer Map(SqlDataReader reader) => new()
    {
        Id = reader.GetGuid(0),
        Name = reader.GetString(1),
        Email = reader.GetString(2)
    };

    public async Task<Customer?> GetByIdAsync(Guid id)
    {
        await using var command = await CreateCommandAsync("SELECT Id, Name, Email FROM Customers WHERE Id = @Id");
        command.Parameters.AddWithValue("@Id", id);

        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? Map(reader) : null;
    }

    public async Task<IEnumerable<Customer>> GetAllAsync()
    {
        var result = new List<Customer>();
        await using var command = await CreateCommandAsync("SELECT Id, Name, Email FROM Customers");

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result.Add(Map(reader));
        return result;
    }

    public async Task AddAsync(Customer customer)
    {
        customer.Id = customer.Id == Guid.Empty ? Guid.NewGuid() : customer.Id;

        await using var command = await CreateCommandAsync(
            "INSERT INTO Customers (Id, Name, Email) VALUES (@Id, @Name, @Email)");
        command.Parameters.AddWithValue("@Id", customer.Id);
        command.Parameters.AddWithValue("@Name", customer.Name);
        command.Parameters.AddWithValue("@Email", customer.Email);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<bool> UpdateAsync(Customer customer)
    {
        await using var command = await CreateCommandAsync(
            "UPDATE Customers SET Name = @Name, Email = @Email WHERE Id = @Id");
        command.Parameters.AddWithValue("@Id", customer.Id);
        command.Parameters.AddWithValue("@Name", customer.Name);
        command.Parameters.AddWithValue("@Email", customer.Email);

        return await command.ExecuteNonQueryAsync() > 0;
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        await using var command = await CreateCommandAsync("DELETE FROM Customers WHERE Id = @Id");
        command.Parameters.AddWithValue("@Id", id);

        return await command.ExecuteNonQueryAsync() > 0;
    }
}