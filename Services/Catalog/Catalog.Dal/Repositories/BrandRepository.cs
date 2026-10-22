using Catalog.Dal.Interfaces;
using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Dal.Repositories;

public class BrandRepository(CatalogDbContext context) : GenericRepository<Brand>(context), IBrandRepository
{
    public Task<bool> ExistsByNameAsync(string name, CancellationToken ct = default)
        => Set.AnyAsync(b => b.Name == name, ct);
}
