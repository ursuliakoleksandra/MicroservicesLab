namespace Catalog.Bll.Dtos;

// ---------- вихідні DTO (лише дані, без логіки) ----------
public record ProductDto(long Id, string Name, string Sku, decimal Price, int AvailableStock);

public record ProductListItemDto(long Id, string Name, string Sku, decimal Price, int AvailableStock,
    bool IsActive, string BrandName, IReadOnlyList<string> Categories);

public record BrandDto(long Id, string Name, string? Country);
public record CategoryDto(long Id, string Name, string Slug);
public record ProductDetailDto(string? Description, int? WeightGrams, int WarrantyMonths, string? Specs);
public record ProductImageDto(long Id, string Url, int SortOrder, bool IsMain);
public record ProductCategoryDto(long CategoryId, string CategoryName, DateTime AddedAt);

public record ProductDetailsDto(long Id, string Name, string Sku, decimal Price, int AvailableStock, bool IsActive,
    DateTime CreatedAt, DateTime? UpdatedAt, BrandDto Brand, ProductDetailDto? Detail,
    IReadOnlyList<ProductImageDto> Images, IReadOnlyList<ProductCategoryDto> Categories);

public record CategoryProductDto(long ProductId, string Name, decimal Price, DateTime AddedAt);
public record CategoryWithProductsDto(long Id, string Name, string Slug, IReadOnlyList<CategoryProductDto> Products);

/// <summary>Контракт відповіді колекції: items + totalCount, page, pageSize, totalPages, hasNext, hasPrevious.</summary>
public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNext => Page < TotalPages;
    public bool HasPrevious => Page > 1;
}
