using Ardalis.Specification;
using Catalog.Domain.Entities;

namespace Catalog.Dal.Interfaces;

public interface IBrandRepository : IGenericRepository<Brand>
{
    Task<bool> ExistsByNameAsync(string name, CancellationToken ct = default);
}

public interface IProductRepository : IGenericRepository<Product>
{
    /// <summary>EAGER LOADING: товар з брендом, деталями, зображеннями та категоріями — ОДИН запит (JOIN).</summary>
    Task<Product?> GetWithDetailsAsync(long id, CancellationToken ct = default);

    Task<bool> ExistsBySkuAsync(string sku, CancellationToken ct = default);

    /// <summary>Застосовує специфікацію (фільтр/сортування/include/Skip/Take) — репозиторій не знає її критеріїв.</summary>
    Task<IReadOnlyList<Product>> ListAsync(ISpecification<Product> spec, CancellationToken ct = default);

    /// <summary>Рахує записи за критеріями специфікації на рівні БД (SELECT COUNT).</summary>
    Task<int> CountAsync(ISpecification<Product> spec, CancellationToken ct = default);
}

public interface ICategoryRepository : IGenericRepository<Category>
{
    /// <summary>EXPLICIT LOADING: категорія — 1-й запит; товари довантажуються окремо — 2-й запит.</summary>
    Task<Category?> GetWithProductsAsync(long id, CancellationToken ct = default);

    Task<Category?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<bool> ExistsByNameOrSlugAsync(string name, string slug, CancellationToken ct = default);
}

/// <summary>Репозиторій проміжної сутності M:N (складений ключ — тому без узагальненого GetByIdAsync).</summary>
public interface IProductCategoryRepository
{
    /// <summary>LINQ to Entities: вибірка M:N через проміжну сутність за її власним полем AddedAt.</summary>
    Task<IReadOnlyList<Product>> GetRecentlyAddedAsync(string categoryName, DateTime since, CancellationToken ct = default);

    Task<ProductCategory?> FindAsync(long productId, long categoryId, CancellationToken ct = default);
    Task AddAsync(ProductCategory link, CancellationToken ct = default);
    void Delete(ProductCategory link);
}
