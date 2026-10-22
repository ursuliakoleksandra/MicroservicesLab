using Catalog.Dal;
using Catalog.Dal.Repositories;
using Catalog.Dal.Specifications;
using Catalog.Domain.Entities;
using Catalog.Domain.Filters;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Catalog.Tests;

/// <summary>Специфікації й репозиторій перевіряються на EF Core InMemory (SQL Server не потрібен).</summary>
public class SpecificationTests
{
    private static async Task<CatalogDbContext> CreateContextAsync()
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new CatalogDbContext(options);
        await db.Database.EnsureCreatedAsync();             // HasData додає бренд Id = 1 (Nova)

        for (var i = 1; i <= 12; i++)
        {
            var p = new Product(1, $"Item {i:00}", $"SKU-{i:00}", i * 10m);
            if (i % 4 != 0) p.AddStock(i);                   // кожен 4-й товар — без залишку
            db.Products.Add(p);
        }
        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task PageSpec_ReturnsRequestedPage_And_CountIgnoresPaging()
    {
        await using var db = await CreateContextAsync();
        var repo = new ProductRepository(db);
        var filter = new ProductFilter { SortBy = ProductSortFields.Price, Descending = false };

        var total = await repo.CountAsync(new ProductsFilterSpec(filter));
        var page2 = await repo.ListAsync(new ProductsPageSpec(filter, skip: 5, take: 5));

        Assert.Equal(12, total);                                        // totalCount — по всій вибірці, не по сторінці
        Assert.Equal(5, page2.Count);
        Assert.Equal("SKU-06", page2[0].Sku);                           // сортування за ціною, 2-га сторінка
    }

    [Fact]
    public async Task PageSpec_Filters_By_PriceRange_And_Search()
    {
        await using var db = await CreateContextAsync();
        var repo = new ProductRepository(db);
        var filter = new ProductFilter { MinPrice = 30m, MaxPrice = 60m, Search = "Item" };

        var items = await repo.ListAsync(new ProductsPageSpec(filter, 0, 50));

        Assert.Equal(4, items.Count);                                   // 30, 40, 50, 60
        Assert.All(items, p => Assert.InRange(p.Price, 30m, 60m));
    }

    [Fact]
    public async Task PageSpec_SortsDescendingByName()
    {
        await using var db = await CreateContextAsync();
        var repo = new ProductRepository(db);
        var filter = new ProductFilter { SortBy = ProductSortFields.Name, Descending = true };

        var items = await repo.ListAsync(new ProductsPageSpec(filter, 0, 3));

        Assert.Equal(new[] { "Item 12", "Item 11", "Item 10" }, items.Select(p => p.Name).ToArray());
    }

    [Fact]
    public async Task AnotherSpecification_WorksThroughSameRepositoryMethod()
    {
        await using var db = await CreateContextAsync();
        var repo = new ProductRepository(db);

        var low = await repo.ListAsync(new LowStockProductsSpec(threshold: 3));

        Assert.All(low, p => Assert.True(p.AvailableStock <= 3));
        Assert.Equal(low.OrderBy(p => p.AvailableStock).Select(p => p.Id), low.Select(p => p.Id));
    }
}
