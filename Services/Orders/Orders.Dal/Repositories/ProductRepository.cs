using Dapper;
using Microsoft.Data.SqlClient;
using Orders.Dal.Interfaces;
using Orders.Domain.Exceptions;
using Orders.Domain.Models;

namespace Orders.Dal.Repositories;

public sealed class ProductRepository : BaseDapperRepository<Product>, IProductRepository
{
    public ProductRepository(SqlConnection connection, Func<SqlTransaction?> currentTransaction)
        : base(connection, currentTransaction, "dbo.Products", identityKey: false,
               dbGeneratedColumns: [nameof(Product.SyncedAt)])   // Id задає Каталог, а не IDENTITY
    { }

    public async Task<IReadOnlyList<Product>> GetByIdsAsync(IReadOnlyCollection<long> ids, CancellationToken ct = default)
    {
        var sql = $"SELECT {SelectColumns} FROM dbo.Products WHERE Id IN @Ids";   // Dapper розгортає список у параметри
        await EnsureOpenAsync(ct);
        return (await Connection.QueryAsync<Product>(Command(sql, new { Ids = ids }, ct: ct))).AsList();
    }

    public async Task DecreaseStockAsync(long productId, int quantity, CancellationToken ct = default)
    {
        // умова Stock >= @Quantity робить зменшення атомарним: два паралельні запити не «підуть в мінус»
        const string sql = "UPDATE dbo.Products SET Stock = Stock - @Quantity WHERE Id = @ProductId AND Stock >= @Quantity";
        await EnsureOpenAsync(ct);
        var affected = await Translated(() => Connection.ExecuteAsync(
            Command(sql, new { ProductId = productId, Quantity = quantity }, ct: ct)));
        if (affected > 0) return;

        var exists = await Connection.ExecuteScalarAsync<int>(
            Command("SELECT COUNT(1) FROM dbo.Products WHERE Id = @ProductId", new { ProductId = productId }, ct: ct));
        throw exists == 0
            ? new NotFoundException($"Product {productId} not found")
            : new BusinessConflictException($"Insufficient stock for product {productId}");
    }

    public async Task IncreaseStockAsync(long productId, int quantity, CancellationToken ct = default)
    {
        const string sql = "UPDATE dbo.Products SET Stock = Stock + @Quantity WHERE Id = @ProductId";
        await EnsureOpenAsync(ct);
        var affected = await Translated(() => Connection.ExecuteAsync(
            Command(sql, new { ProductId = productId, Quantity = quantity }, ct: ct)));
        if (affected == 0) throw new NotFoundException($"Product {productId} not found");
    }

    public async Task<bool> ApplyCatalogChangeAsync(long productId, string? name, decimal? price, bool? isActive,
        DateTime occurredAt, CancellationToken ct = default)
    {
        // Кожне поле має свою «версію» (…ChangedAt). CASE застосовує нове значення лише коли подія новіша; інакше поле лишається.
        // Умова WHERE Id = @Id: рядок «знайдено» навіть якщо всі поля застарілі — відрізняємо «немає товару» від «подія застаріла».
        const string sql = @"
            UPDATE dbo.Products SET
                Name            = CASE WHEN @Name IS NOT NULL AND (NameChangedAt IS NULL OR NameChangedAt < @At) THEN @Name ELSE Name END,
                NameChangedAt   = CASE WHEN @Name IS NOT NULL AND (NameChangedAt IS NULL OR NameChangedAt < @At) THEN @At ELSE NameChangedAt END,
                Price           = CASE WHEN @Price IS NOT NULL AND (PriceChangedAt IS NULL OR PriceChangedAt < @At) THEN @Price ELSE Price END,
                PriceChangedAt  = CASE WHEN @Price IS NOT NULL AND (PriceChangedAt IS NULL OR PriceChangedAt < @At) THEN @At ELSE PriceChangedAt END,
                IsActive        = CASE WHEN @IsActive IS NOT NULL AND (ActiveChangedAt IS NULL OR ActiveChangedAt < @At) THEN @IsActive ELSE IsActive END,
                ActiveChangedAt = CASE WHEN @IsActive IS NOT NULL AND (ActiveChangedAt IS NULL OR ActiveChangedAt < @At) THEN @At ELSE ActiveChangedAt END,
                SyncedAt        = SYSUTCDATETIME()
            WHERE Id = @Id;";
        await EnsureOpenAsync(ct);
        var affected = await Translated(() => Connection.ExecuteAsync(Command(sql,
            new { Id = productId, Name = name, Price = price, IsActive = isActive, At = occurredAt }, ct: ct)));
        return affected > 0;
    }

    public async Task<bool> InsertFromCatalogAsync(long productId, string name, decimal price, bool isActive, int stock,
        DateTime occurredAt, CancellationToken ct = default)
    {
        // Лише INSERT, якщо рядка немає: подія product.created не перезаписує наявні дані й залишок Stock (його веде Orders).
        const string sql = @"
            IF NOT EXISTS (SELECT 1 FROM dbo.Products WITH (UPDLOCK, HOLDLOCK) WHERE Id = @Id)
            BEGIN
                INSERT INTO dbo.Products (Id, Name, Price, IsActive, Stock, NameChangedAt, PriceChangedAt, ActiveChangedAt)
                VALUES (@Id, @Name, @Price, @IsActive, @Stock, @At, @At, @At);
                SELECT 1;
            END
            ELSE SELECT 0;";
        await EnsureOpenAsync(ct);
        return await Translated(() => Connection.ExecuteScalarAsync<int>(Command(sql,
            new { Id = productId, Name = name, Price = price, IsActive = isActive, Stock = stock, At = occurredAt }, ct: ct))) == 1;
    }

    public async Task UpsertFromCatalogAsync(Product product, CancellationToken ct = default)
    {
        const string sql = @"
            UPDATE dbo.Products
            SET Name = @Name, Price = @Price, IsActive = @IsActive, SyncedAt = SYSUTCDATETIME()
            WHERE Id = @Id;
            IF @@ROWCOUNT = 0
                INSERT INTO dbo.Products (Id, Name, Price, IsActive, Stock)
                VALUES (@Id, @Name, @Price, @IsActive, @Stock);";
        await EnsureOpenAsync(ct);
        await Translated(() => Connection.ExecuteAsync(Command(sql,
            new { product.Id, product.Name, product.Price, product.IsActive, product.Stock }, ct: ct)));
    }
}
