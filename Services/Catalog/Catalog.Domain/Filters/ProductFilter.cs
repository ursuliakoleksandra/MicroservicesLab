namespace Catalog.Domain.Filters;

/// <summary>Критерії вибірки товарів (не залежать від HTTP і EF) — вхід для специфікацій.</summary>
public class ProductFilter
{
    public string? Search { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public long? CategoryId { get; init; }
    public long? BrandId { get; init; }
    public bool? InStock { get; init; }
    public string SortBy { get; init; } = ProductSortFields.Name;
    public bool Descending { get; init; }
}

/// <summary>Дозволені поля сортування (білий список).</summary>
public static class ProductSortFields
{
    public const string Name = "name";
    public const string Price = "price";
    public const string Sku = "sku";
    public const string Created = "created";
    public const string Stock = "stock";

    public static readonly IReadOnlyList<string> All = [Name, Price, Sku, Created, Stock];
}
