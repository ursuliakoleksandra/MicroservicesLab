using Ardalis.Specification;
using Catalog.Domain.Entities;
using Catalog.Domain.Filters;

namespace Catalog.Dal.Specifications;

/// <summary>Спільні критерії фільтрації (використовуються і для списку, і для підрахунку totalCount).</summary>
internal static class ProductCriteria
{
    public static void Apply(ISpecificationBuilder<Product> query, ProductFilter f)
    {
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var search = f.Search.Trim();
            query.Where(p => p.Name.Contains(search) || p.Sku.Contains(search));
        }
        if (f.MinPrice is { } min) query.Where(p => p.Price >= min);
        if (f.MaxPrice is { } max) query.Where(p => p.Price <= max);
        if (f.BrandId is { } brandId) query.Where(p => p.BrandId == brandId);
        if (f.CategoryId is { } categoryId) query.Where(p => p.ProductCategories.Any(pc => pc.CategoryId == categoryId));
        if (f.InStock is true) query.Where(p => p.AvailableStock > 0);
        if (f.InStock is false) query.Where(p => p.AvailableStock == 0);
    }
}

/// <summary>Лише критерії (Where) — без Include/OrderBy/Skip/Take: для SELECT COUNT(*).</summary>
public sealed class ProductsFilterSpec : Specification<Product>
{
    public ProductsFilterSpec(ProductFilter filter) => ProductCriteria.Apply(Query, filter);
}

/// <summary>Повна специфікація сторінки: Where + Include + OrderBy + Skip/Take (виконується одним SQL-запитом).</summary>
public sealed class ProductsPageSpec : Specification<Product>
{
    public ProductsPageSpec(ProductFilter filter, int skip, int take)
    {
        Query.AsNoTracking();
        Query.Include(p => p.Brand);
        Query.Include(p => p.ProductCategories).ThenInclude(pc => pc.Category);

        ProductCriteria.Apply(Query, filter);

        // сортування — лише за дозволеними полями; Id як другий ключ робить порядок сторінок стабільним
        switch (filter.SortBy, filter.Descending)
        {
            case (ProductSortFields.Price, true): Query.OrderByDescending(p => p.Price).ThenBy(p => p.Id); break;
            case (ProductSortFields.Price, false): Query.OrderBy(p => p.Price).ThenBy(p => p.Id); break;
            case (ProductSortFields.Sku, true): Query.OrderByDescending(p => p.Sku).ThenBy(p => p.Id); break;
            case (ProductSortFields.Sku, false): Query.OrderBy(p => p.Sku).ThenBy(p => p.Id); break;
            case (ProductSortFields.Created, true): Query.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id); break;
            case (ProductSortFields.Created, false): Query.OrderBy(p => p.CreatedAt).ThenBy(p => p.Id); break;
            case (ProductSortFields.Stock, true): Query.OrderByDescending(p => p.AvailableStock).ThenBy(p => p.Id); break;
            case (ProductSortFields.Stock, false): Query.OrderBy(p => p.AvailableStock).ThenBy(p => p.Id); break;
            case (_, true): Query.OrderByDescending(p => p.Name).ThenBy(p => p.Id); break;
            default: Query.OrderBy(p => p.Name).ThenBy(p => p.Id); break;
        }

        Query.Skip(skip).Take(take);                                  // SQL: OFFSET ... FETCH NEXT
    }
}

/// <summary>ДРУГА специфікація з іншими критеріями: працює через той самий ListAsync без змін у репозиторії.</summary>
public sealed class LowStockProductsSpec : Specification<Product>
{
    public LowStockProductsSpec(int threshold)
    {
        Query.AsNoTracking();
        Query.Include(p => p.Brand);
        Query.Include(p => p.ProductCategories).ThenInclude(pc => pc.Category);
        Query.Where(p => p.IsActive && p.AvailableStock <= threshold);
        Query.OrderBy(p => p.AvailableStock).ThenBy(p => p.Name);
    }
}
