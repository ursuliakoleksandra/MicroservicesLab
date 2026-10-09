using Orders.Domain;

namespace Orders.Dal;

public class ProductRepository : BaseDapperRepository<Product>, IProductRepository
{
    public ProductRepository(DbSession session) : base(session) { }

    protected override string SelectByIdSql => "SELECT Id, Name, Price, Stock FROM Products WHERE Id = @Id";
    protected override string SelectAllSql => "SELECT Id, Name, Price, Stock FROM Products";
    protected override string InsertSql => "INSERT INTO Products (Id, Name, Price, Stock) VALUES (@Id, @Name, @Price, @Stock)";
    protected override string UpdateSql => "UPDATE Products SET Name = @Name, Price = @Price, Stock = @Stock WHERE Id = @Id";
    protected override string DeleteSql => "DELETE FROM Products WHERE Id = @Id";

    protected override void EnsureId(Product entity)
    {
        if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();
    }

    public async Task<bool> TryDecreaseStockAsync(Guid productId, int quantity) =>
        await ExecuteAsync(
            "UPDATE Products SET Stock = Stock - @Quantity WHERE Id = @Id AND Stock >= @Quantity",
            new { Id = productId, Quantity = quantity }) > 0;
}