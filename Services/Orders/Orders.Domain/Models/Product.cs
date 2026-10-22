namespace Orders.Domain.Models;

/// <summary>Локальна копія товару з Каталогу (власник назви й ціни — CatalogDb). Id збігається з Каталогом.</summary>
public class Product
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
    public int Stock { get; set; }
    public DateTime SyncedAt { get; set; }
}
