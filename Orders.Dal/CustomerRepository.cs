using Microsoft.Data.SqlClient;
using Orders.Domain;

namespace Orders.Dal;

public class CustomerRepository : ICustomerRepository
{
    private readonly string _connectionString;

    public CustomerRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<Customer?> GetByIdAsync(Guid id)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new SqlCommand("SELECT Id, Name, Email FROM Customers WHERE Id = @Id", connection);
        command.Parameters.AddWithValue("@Id", id);

        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new Customer
            {
                Id = reader.GetGuid(0),
                Name = reader.GetString(1),
                Email = reader.GetString(2)
            };
        }

        return null;
    }

    public async Task<Guid> CreateAsync(Customer customer)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        var command = new SqlCommand(
            "INSERT INTO Customers (Id, Name, Email) VALUES (@Id, @Name, @Email)", connection);

        customer.Id = customer.Id == Guid.Empty ? Guid.NewGuid() : customer.Id;
        command.Parameters.AddWithValue("@Id", customer.Id);
        command.Parameters.AddWithValue("@Name", customer.Name);
        command.Parameters.AddWithValue("@Email", customer.Email);

        await command.ExecuteNonQueryAsync();
        return customer.Id;
    }
}