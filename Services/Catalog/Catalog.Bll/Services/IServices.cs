using Catalog.Bll.Dtos;

namespace Catalog.Bll.Services;

public interface IProductService
{
    Task<PagedResult<ProductListItemDto>> GetListAsync(ProductQuery query, CancellationToken ct);
    Task<IReadOnlyList<ProductListItemDto>> GetLowStockAsync(int threshold, CancellationToken ct);
    Task<ProductDetailsDto> GetByIdAsync(long id, CancellationToken ct);
    Task<ProductDto> CreateAsync(CreateProductDto input, CancellationToken ct);
    Task<ProductDto> UpdateAsync(long id, UpdateProductDto input, CancellationToken ct);
    Task<ProductDto> ChangePriceAsync(long id, ChangePriceDto input, CancellationToken ct);
    Task<ProductDto> AddStockAsync(long id, StockChangeDto input, CancellationToken ct);
    Task<ProductDto> RemoveStockAsync(long id, StockChangeDto input, CancellationToken ct);
    Task<ProductDetailsDto> SaveDetailAsync(long id, SaveProductDetailDto input, CancellationToken ct);
    Task<ProductDetailsDto> AddImageAsync(long id, AddProductImageDto input, CancellationToken ct);
    Task AssignCategoryAsync(long productId, long categoryId, CancellationToken ct);
    Task RemoveCategoryAsync(long productId, long categoryId, CancellationToken ct);
    Task DeleteAsync(long id, CancellationToken ct);
}

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken ct);
    Task<CategoryDto> GetByIdAsync(long id, CancellationToken ct);
    Task<CategoryWithProductsDto> GetWithProductsAsync(long id, CancellationToken ct);
    Task<IReadOnlyList<ProductDto>> GetRecentlyAddedProductsAsync(string categoryName, DateTime? since, CancellationToken ct);
    Task<CategoryDto> CreateAsync(CreateCategoryDto input, CancellationToken ct);
}

public interface IBrandService
{
    Task<IReadOnlyList<BrandDto>> GetAllAsync(CancellationToken ct);
    Task<BrandDto> CreateAsync(CreateBrandDto input, CancellationToken ct);
}
