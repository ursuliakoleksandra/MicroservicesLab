using Catalog.Infrastructure;
using Reviews.Infrastructure;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver; // Додано для підтримки методів MongoDB (Find, ToListAsync)

Console.WriteLine("=== ПЕРЕВІРКА МІКРОСЕРВІСНИХ БАЗ ДАНИХ ===");

// 1. Перевірка SQL Server (Catalog.Infrastructure)
Console.WriteLine("\n[1] Перевірка SQL Server (CatalogDb)...");
await using (var sqlContext = new CatalogDbContext())
{
    var products = await sqlContext.Products
        .Include(p => p.Details)
        .Include(p => p.Category)
        .ToListAsync();

    foreach (var p in products)
    {
        Console.WriteLine($"-> Знайдено товар: {p.Name} | Категорія: {p.Category.Name} | Ціна: {p.Price} грн");
        Console.WriteLine($"   Деталі (1:1): {p.Details.Description}");
    }
}

// 2. Перевірка та ініціалізація MongoDB (Reviews.Infrastructure)
Console.WriteLine("\n[2] Перевірка MongoDB (ReviewsDb)...");
var mongoContext = new ReviewsDbContext();
await mongoContext.SeedAndIndexDataAsync();

var reviewsCursor = await mongoContext.Reviews.FindAsync(FilterDefinition<ProductReview>.Empty);
var reviews = await reviewsCursor.ToListAsync();

foreach (var r in reviews)
{
    Console.WriteLine($"-> Відгук від '{r.CustomerName}': \"{r.Comment}\" (Оцінка: {r.Rating}/5)");
}

Console.WriteLine("\n=== УСІ БАЗИ ДАНИХ ПРАЦЮЮТЬ ІДЕАЛЬНО! ===");