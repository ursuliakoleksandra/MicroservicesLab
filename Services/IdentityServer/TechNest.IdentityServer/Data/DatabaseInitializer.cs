using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Data.SqlClient;

namespace TechNest.IdentityServer.Data;

/// <summary>
/// Ініціалізація схеми БД. Основний шлях — міграції EF Core (<c>dotnet ef migrations add</c>; команди — scripts/add-migrations.ps1).
/// Якщо міграцій у збірці ще немає (перший запуск до їх генерації), таблиці контексту створюються з моделі:
/// кілька контекстів в одній БД не можна ініціалізувати через EnsureCreated (він пропускає БД, де вже є таблиці), тому
/// використовується CreateTables для кожного контексту окремо.
/// </summary>
public static class DatabaseInitializer
{
    private const int ObjectAlreadyExists = 2714;

    public static async Task ApplyAsync(DbContext db, CancellationToken ct = default)
    {
        if (db.Database.GetMigrations().Any())
        {
            await db.Database.MigrateAsync(ct);
            return;
        }

        var creator = db.GetService<IRelationalDatabaseCreator>();
        if (!await creator.ExistsAsync(ct)) await creator.CreateAsync(ct);
        try { await creator.CreateTablesAsync(ct); }
        catch (SqlException ex) when (ex.Number == ObjectAlreadyExists) { /* таблиці цього контексту вже створено */ }
    }
}
