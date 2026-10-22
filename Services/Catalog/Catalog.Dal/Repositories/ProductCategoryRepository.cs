using Catalog.Dal.Interfaces;
using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Dal.Repositories;

public class ProductCategoryRepository(CatalogDbContext context)
    : GenericRepository<ProductCategory>(context), IProductCategoryRepository
{
    // складений ключ: пошук за одним Id неможливий
    public override Task<ProductCategory?> GetByIdAsync(long id, CancellationToken ct = default)
        => throw new NotSupportedException("ProductCategory has a composite key; use FindAsync(productId, categoryId).");

    // LINQ to Entities: M:N через проміжну сутність і її власне поле AddedAt
    public async Task<IReadOnlyList<Product>> GetRecentlyAddedAsync(
        string categoryName, DateTime since, CancellationToken ct = default)
        => await Set
            .Where(pc => pc.Category.Name == categoryName && pc.AddedAt >= since)
            .OrderByDescending(pc => pc.AddedAt)
            .Select(pc => pc.Product)
            .AsNoTracking()
            .ToListAsync(ct);

    public Task<ProductCategory?> FindAsync(long productId, long categoryId, CancellationToken ct = default)
        => Set.FirstOrDefaultAsync(pc => pc.ProductId == productId && pc.CategoryId == categoryId, ct);
}
