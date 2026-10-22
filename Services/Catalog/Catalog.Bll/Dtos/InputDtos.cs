using System.ComponentModel.DataAnnotations;

namespace Catalog.Bll.Dtos;

// Прості структурні правила (Required, StringLength, Range) — DataAnnotations;
// формат і складніші правила — FluentValidation (папка Validators).

/// <summary>Параметри запиту колекції: пагінація, фільтри, сортування (виконуються на рівні SQL).</summary>
public class ProductQuery
{
    [Range(1, 1_000_000)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public long? CategoryId { get; set; }
    public long? BrandId { get; set; }
    public bool? InStock { get; set; }
    public string SortBy { get; set; } = "name";      // name | price | sku | created | stock
    public string SortDir { get; set; } = "asc";      // asc | desc
}

public class CreateProductDto
{
    [Range(1, long.MaxValue)] public long BrandId { get; set; }
    [Required, StringLength(200)] public string Name { get; set; } = "";
    [Required, StringLength(50)] public string Sku { get; set; } = "";
    public decimal Price { get; set; }
    [Range(0, 1_000_000)] public int InitialStock { get; set; }
}

public class UpdateProductDto
{
    [Required, StringLength(200)] public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public class ChangePriceDto
{
    public decimal Price { get; set; }
}

public class StockChangeDto
{
    [Range(1, 1_000_000)] public int Quantity { get; set; }
}

public class SaveProductDetailDto
{
    [StringLength(2000)] public string? Description { get; set; }
    public int? WeightGrams { get; set; }
    [Range(0, 600)] public int WarrantyMonths { get; set; } = 12;
    public string? Specs { get; set; }                // JSON
}

public class AddProductImageDto
{
    [Required, StringLength(500)] public string Url { get; set; } = "";
    [Range(0, 1000)] public int SortOrder { get; set; }
    public bool IsMain { get; set; }
}

public class CreateCategoryDto
{
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [Required, StringLength(100)] public string Slug { get; set; } = "";
}

public class CreateBrandDto
{
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [StringLength(100)] public string? Country { get; set; }
}
