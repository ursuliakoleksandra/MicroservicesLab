using Catalog.Dal.Interfaces;
using Catalog.Dal.Repositories;
using Catalog.Domain.Exceptions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Dal;

/// <summary>
/// Unit of Work: усі репозиторії працюють з ОДНИМ DbContext; SaveChangesAsync викликається один раз
/// наприкінці бізнес-операції й зберігає всі зміни в одній транзакції.
/// (В eShop цю роль виконує сам DbContext — OrderingContext : IUnitOfWork.)
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly CatalogDbContext _context;

    public UnitOfWork(CatalogDbContext context)
    {
        _context = context;
        Brands = new BrandRepository(context);
        Categories = new CategoryRepository(context);
        Products = new ProductRepository(context);
        ProductCategories = new ProductCategoryRepository(context);
    }

    public IBrandRepository Brands { get; }
    public ICategoryRepository Categories { get; }
    public IProductRepository Products { get; }
    public IProductCategoryRepository ProductCategories { get; }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            return await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("The record was modified by another request. Reload it and try again.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // гонка: перевірка унікальності пройшла, але інший запит устиг вставити такий самий ключ
            throw new ConflictException("Record with the same unique key already exists");
        }
    }
}
