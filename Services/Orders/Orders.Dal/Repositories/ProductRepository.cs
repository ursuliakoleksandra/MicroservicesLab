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
