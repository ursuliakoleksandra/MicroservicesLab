namespace Catalog.Dal.Interfaces;

/// <summary>Об'єднує репозиторії; SaveChangesAsync зберігає ВСІ зміни однією транзакцією.</summary>
public interface IUnitOfWork
{
    IBrandRepository Brands { get; }
    ICategoryRepository Categories { get; }
    IProductRepository Products { get; }
    IProductCategoryRepository ProductCategories { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
