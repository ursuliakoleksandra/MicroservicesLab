using Catalog.Domain.Exceptions;

namespace Catalog.Domain.Entities;

/// <summary>
/// Агрегат «Товар» (аналог CatalogItem в eShop). Стан змінюється лише методами сутності —
/// відкритих setter-ів немає; інваріанти (ціна &gt; 0, достатній залишок) перевіряє сама сутність.
/// </summary>
public class Product
{
    public long Id { get; private set; }
    public long BrandId { get; private set; }
    public Brand Brand { get; private set; } = null!;
    public string Name { get; private set; } = "";
    public string Sku { get; private set; } = "";
    public decimal Price { get; private set; }
    public int AvailableStock { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();   // оптимістична конкурентність

    public ProductDetail? Detail { get; private set; }                      // 1:1
    public List<ProductImage> Images { get; } = new();                       // 1:N
    public List<ProductCategory> ProductCategories { get; } = new();         // M:N

    private Product() { }                                                    // для EF Core

    public Product(long brandId, string name, string sku, decimal price)
    {
        if (brandId <= 0) throw new DomainException("Brand is required");
        BrandId = brandId;
        Name = Guard.NotBlank(name, "Name", 200);
        Sku = Guard.Sku(sku);
        Price = Guard.Price(price);
        CreatedAt = DateTime.UtcNow;
    }

    public void Rename(string name)
    {
        Name = Guard.NotBlank(name, "Name", 200);
        Touch();
    }

    public void ChangePrice(decimal newPrice)
    {
        Price = Guard.Price(newPrice);
        Touch();
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        Touch();
    }

    public void RemoveStock(int quantity)
    {
        if (quantity <= 0) throw new DomainException("Quantity must be positive");
        if (AvailableStock < quantity) throw new DomainException($"Only {AvailableStock} units available");
        AvailableStock -= quantity;
        Touch();
    }

    public void AddStock(int quantity)
    {
        if (quantity <= 0) throw new DomainException("Quantity must be positive");
        if (AvailableStock > int.MaxValue - quantity) throw new DomainException("Stock limit exceeded");
        AvailableStock += quantity;
        Touch();
    }

    public void SetDetail(string? description, int? weightGrams, int warrantyMonths, string? specsJson)
    {
        if (Detail is null) Detail = new ProductDetail(description, weightGrams, warrantyMonths, specsJson);
        else Detail.Update(description, weightGrams, warrantyMonths, specsJson);
        Touch();
    }

    public ProductImage AddImage(string url, int sortOrder, bool isMain)
    {
        if (isMain && Images.Any(i => i.IsMain))
            throw new DomainException("Product already has a main image");
        var image = new ProductImage(url, sortOrder, isMain);
        Images.Add(image);
        return image;
    }

    public void AssignToCategory(long categoryId)
    {
        if (ProductCategories.Any(pc => pc.CategoryId == categoryId))
            throw new DomainException("Product is already in this category");
        ProductCategories.Add(new ProductCategory(Id, categoryId));
    }

    private void Touch() => UpdatedAt = DateTime.UtcNow;
}
