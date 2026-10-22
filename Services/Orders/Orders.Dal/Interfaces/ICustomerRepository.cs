using Orders.Domain.Models;

namespace Orders.Dal.Interfaces;

/// <summary>
/// Репозиторій на ЧИСТОМУ ADO.NET. Свідомо НЕ успадковує IGenericRepository&lt;T&gt;:
/// демонструє ручну роботу з SqlCommand/SqlDataReader (потрібна власна семантика Update та SoftDelete).
/// </summary>
public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<Customer?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<IReadOnlyList<Customer>> GetPagedAsync(int page, int pageSize, CancellationToken ct = default);
    Task<long> AddAsync(Customer customer, string? createdBy, CancellationToken ct = default);
    Task<bool> UpdateAsync(Customer customer, string? updatedBy, CancellationToken ct = default);
    Task<bool> SoftDeleteAsync(long id, string? deletedBy, CancellationToken ct = default);
}
