using Orders.Domain;

namespace Orders.Dal;

public interface IGenericRepository<T> where T : class
{
    Task<T?> GetByIdAsync(Guid id);
    Task<IEnumerable<T>> GetAllAsync();
    Task AddAsync(T entity);
    Task<bool> UpdateAsync(T entity);
    Task<bool> DeleteAsync(Guid id);
}

public interface ICustomerRepository : IGenericRepository<Customer> { }

public interface IProductRepository : IGenericRepository<Product>
{
    Task<bool> TryDecreaseStockAsync(Guid productId, int quantity);
}

public interface IOrderRepository : IGenericRepository<Order> { }