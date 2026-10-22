using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Dal.Seed;

/// <summary>
/// ПРОГРАМНИЙ ідемпотентний сідінг після MigrateAsync(): кожен товар створюється, лише якщо його SKU ще немає.
/// Довідники (бренди й категорії з фіксованими Id) уже додані міграцією через HasData.
/// Товари додаються по одному, тому на порожній БД отримують Id 1..5 — як у OrdersDb та ReviewsDb.
/// </summary>
public static class DbInitializer
{
    private sealed record SeedProduct(string Sku, long BrandId, string Name, decimal Price, int Stock,
        string Description, int WeightGrams, int WarrantyMonths, string? Specs, long[] CategoryIds, string[] Images);

    private static readonly SeedProduct[] Products =
    [
        new("NB-X15", 1, "Ноутбук X15", 32999.00m, 10, "15.6\" ноутбук для роботи й ігор", 1800, 24,
            "{\"cpu\":\"8 cores\",\"ram\":\"16 GB\",\"ssd\":\"512 GB\"}", [1, 6], ["/img/nb-x15-1.jpg", "/img/nb-x15-2.jpg"]),
        new("PH-S10", 2, "Смартфон S10", 18999.00m, 25, "Смартфон з OLED-екраном", 180, 12,
            "{\"display\":\"6.5 OLED\",\"battery\":\"5000 mAh\"}", [2], ["/img/ph-s10-1.jpg"]),
        new("AU-AIR", 3, "Навушники Air", 3499.00m, 50, "Бездротові навушники з ANC", 55, 12,
            "{\"bluetooth\":\"5.3\",\"anc\":true}", [3, 4], ["/img/au-air-1.jpg"]),
        new("MS-M7", 4, "Миша ігрова M7", 1299.00m, 40, "Ігрова миша 26000 DPI", 85, 6, null, [4, 6], ["/img/ms-m7-1.jpg"]),
        new("MN-27Q", 5, "Монітор 27\" QHD", 11999.00m, 5, "27\" QHD IPS-монітор 165 Гц", 5200, 36,
            "{\"resolution\":\"2560x1440\",\"refreshRate\":165}", [5, 6], ["/img/mn-27q-1.jpg"])
    ];

    public static async Task SeedAsync(CatalogDbContext db, CancellationToken ct = default)
    {
        foreach (var s in Products)
        {
            if (await db.Products.AnyAsync(p => p.Sku == s.Sku, ct)) continue;     // уже наповнено — дублів не буде

            var product = new Product(s.BrandId, s.Name, s.Sku, s.Price);
            product.AddStock(s.Stock);
            product.SetDetail(s.Description, s.WeightGrams, s.WarrantyMonths, s.Specs);
            for (var i = 0; i < s.Images.Length; i++) product.AddImage(s.Images[i], i, isMain: i == 0);
            foreach (var categoryId in s.CategoryIds) product.AssignToCategory(categoryId);

            db.Products.Add(product);
            await db.SaveChangesAsync(ct);                                          // по одному: детермінований порядок Id
        }
    }
}
