using Ardalis.Specification;
using Ardalis.Specification.EntityFrameworkCore;
using Catalog.Dal.Interfaces;
using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Dal.Repositories;

public class ProductRepository(CatalogDbContext context) : GenericRepository<Product>(context), IProductRepository
{
    // EAGER LOADING — один SQL-запит із JOIN-ами: товар + бренд + деталі + зображення + категорії
    public Task<Product?> GetWithDetailsAsync(long id, CancellationToken ct = default)
        => Set.Include(p => p.Brand)
              .Include(p => p.Detail)
              .Include(p => p.Images)
              .Include(p => p.ProductCategories).ThenInclude(pc => pc.Category)
              .FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<bool> ExistsBySkuAsync(string sku, CancellationToken ct = default)
        => Set.AnyAsync(p => p.Sku == sku, ct);

    // Specification pattern: репозиторій лише застосовує специфікацію, а критерії описані окремо
    public async Task<IReadOnlyList<Product>> ListAsync(ISpecification<Product> spec, CancellationToken ct = default)
        => await Set.WithSpecification(spec).ToListAsync(ct);

    public Task<int> CountAsync(ISpecification<Product> spec, CancellationToken ct = default)
        => Set.WithSpecification(spec).CountAsync(ct);
}
