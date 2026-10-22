using Catalog.Domain.Exceptions;

namespace Catalog.Domain.Entities;

/// <summary>1:N: зображення товару.</summary>
public class ProductImage
{
    public long Id { get; private set; }
    public long ProductId { get; private set; }
    public string Url { get; private set; } = "";
    public int SortOrder { get; private set; }
    public bool IsMain { get; private set; }

    private ProductImage() { }

    public ProductImage(string url, int sortOrder, bool isMain)
    {
        Url = Guard.NotBlank(url, "Image url", 500);
        if (sortOrder < 0) throw new DomainException("SortOrder cannot be negative");
        SortOrder = sortOrder;
        IsMain = isMain;
    }
}
