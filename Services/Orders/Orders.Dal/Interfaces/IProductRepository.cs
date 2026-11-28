using Orders.Domain.Models;

namespace Orders.Dal.Interfaces;

public interface IProductRepository : IGenericRepository<Product>
{
    Task<IReadOnlyList<Product>> GetByIdsAsync(IReadOnlyCollection<long> ids, CancellationToken ct = default);

    /// <summary>Атомарно зменшує залишок; якщо товару замало — BusinessConflictException.</summary>
    Task DecreaseStockAsync(long productId, int quantity, CancellationToken ct = default);

    /// <summary>Повертає залишок (скасування підтвердженого замовлення).</summary>
    Task IncreaseStockAsync(long productId, int quantity, CancellationToken ct = default);

    /// <summary>
    /// Лаб. 08: застосовує зміну з події Каталогу до локальної копії. Кожне поле (назва, ціна, активність) оновлюється лише
    /// якщо <paramref name="occurredAt"/> новіший за час його останнього застосування — застарілі події не затирають новіші.
    /// Залишок Stock не змінюється. Повертає false, якщо товару в Orders ще немає.
    /// </summary>
    Task<bool> ApplyCatalogChangeAsync(long productId, string? name, decimal? price, bool? isActive, DateTime occurredAt, CancellationToken ct = default);

    /// <summary>Лаб. 08: створює локальну копію товару, якщо її ще немає (подія product.created). Повертає true, якщо рядок додано.</summary>
    Task<bool> InsertFromCatalogAsync(long productId, string name, decimal price, bool isActive, int stock, DateTime occurredAt, CancellationToken ct = default);

    /// <summary>Оновлює локальну копію довідника (імітація події з Каталогу; у Лаб. 08 реплікація виконується обробниками подій (Orders.Bll/Messaging)). Залишок Stock не перезаписується.</summary>
    Task UpsertFromCatalogAsync(Product product, CancellationToken ct = default);
}
