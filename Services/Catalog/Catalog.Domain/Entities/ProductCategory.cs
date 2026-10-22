namespace Catalog.Domain.Entities;

/// <summary>Проміжна сутність M:N Product ↔ Category зі складеним ключем і власним полем AddedAt.</summary>
public class ProductCategory
{
    public long ProductId { get; private set; }
    public Product Product { get; private set; } = null!;
    public long CategoryId { get; private set; }
    public Category Category { get; private set; } = null!;
    public DateTime AddedAt { get; private set; }

    private ProductCategory() { }

    public ProductCategory(long productId, long categoryId)
    {
        ProductId = productId;           // 0 для нового товару: EF проставить значення під час збереження графа
        CategoryId = categoryId;
        AddedAt = DateTime.UtcNow;
    }
}
