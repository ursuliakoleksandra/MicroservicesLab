using Orders.Domain.Models;

namespace Orders.Dal.Interfaces;

public interface IProductRepository : IGenericRepository<Product>
{
    Task<IReadOnlyList<Product>> GetByIdsAsync(IReadOnlyCollection<long> ids, CancellationToken ct = default);

    /// <summary>Атомарно зменшує залишок; якщо товару замало — BusinessConflictException.</summary>
    Task DecreaseStockAsync(long productId, int quantity, CancellationToken ct = default);

    /// <summary>Повертає залишок (скасування підтвердженого замовлення).</summary>
    Task IncreaseStockAsync(long productId, int quantity, CancellationToken ct = default);

    /// <summary>Оновлює локальну копію довідника (імітація події з Каталогу). Залишок Stock не перезаписується.</summary>
    Task UpsertFromCatalogAsync(Product product, CancellationToken ct = default);
}
