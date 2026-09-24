using Orders.Domain;

namespace Orders.Dal;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(Guid id);
    Task<Guid> CreateAsync(Customer customer);
}

public interface IProductRepository
{
    Task<bool> ExistsAsync(Guid id);
    Task<decimal> GetPriceAsync(Guid id);
}

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(Guid id);
    Task CreateAsync(Order order, System.Data.IDbTransaction transaction);
}