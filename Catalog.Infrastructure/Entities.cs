namespace Catalog.Infrastructure;

// 1:1 - Product <-> ProductDetails
// 1:N - Category <-> Product
// M:N - Product <-> Tag (через ProductTag)

public class Product
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    
    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    
    public ProductDetails Details { get; set; } = null!; // Зв'язок 1:1
    public ICollection<ProductTag> ProductTags { get; set; } = new List<ProductTag>(); // Зв'язок M:N
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = null!; // Конкурентність
}

public class ProductDetails
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public string Description { get; set; } = string.Empty;
    public double WeightKg { get; set; }
}

public class Category
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ICollection<Product> Products { get; set; } = new List<Product>();
}

public class Tag
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public ICollection<ProductTag> ProductTags { get; set; } = new List<ProductTag>();
}

public class ProductTag
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public Guid TagId { get; set; }
    public Tag Tag { get; set; } = null!;
}