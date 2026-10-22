using Catalog.Dal.Interfaces;
using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Dal.Repositories;

public class CategoryRepository(CatalogDbContext context) : GenericRepository<Category>(context), ICategoryRepository
{
    // EXPLICIT LOADING — спочатку лише категорія, пов'язані товари — окремим запитом, коли вони справді потрібні
    public async Task<Category?> GetWithProductsAsync(long id, CancellationToken ct = default)
    {
        var category = await Set.FirstOrDefaultAsync(c => c.Id == id, ct);          // 1-й SQL-запит

        if (category is null) return null;

        await Context.Entry(category).Collection(c => c.ProductCategories)
            .Query().Include(pc => pc.Product)
            .LoadAsync(ct);                                                          // 2-й SQL-запит
        return category;
    }

    public Task<Category?> GetByNameAsync(string name, CancellationToken ct = default)
        => Set.AsNoTracking().FirstOrDefaultAsync(c => c.Name == name, ct);

    public Task<bool> ExistsByNameOrSlugAsync(string name, string slug, CancellationToken ct = default)
        => Set.AnyAsync(c => c.Name == name || c.Slug == slug, ct);
}
