using AutoMapper;
using Catalog.Bll.Dtos;
using Catalog.Dal.Interfaces;
using Catalog.Dal.Specifications;
using Catalog.Domain.Entities;
using Catalog.Domain.Exceptions;
using Catalog.Domain.Filters;
using Microsoft.Extensions.Logging;

namespace Catalog.Bll.Services;

/// <summary>
/// Бізнес-логіка каталогу. З EF напряму не працює — лише через репозиторії та Unit of Work.
/// Правила самої сутності (ціна &gt; 0, достатній залишок) перевіряє Product; сервіс — правила, що потребують БД
/// (унікальність SKU, існування бренду/категорії). SaveChangesAsync викликається ОДИН раз наприкінці операції.
/// </summary>
public sealed class ProductService(IUnitOfWork uow, IMapper mapper, ILogger<ProductService> logger) : IProductService
{
    private const int MaxPageSize = 100;

    // ---------- пагінація, фільтрація, сортування — на рівні SQL ----------
    public async Task<PagedResult<ProductListItemDto>> GetListAsync(ProductQuery query, CancellationToken ct)
    {
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
        var page = Math.Max(query.Page, 1);

        var filter = new ProductFilter
        {
            Search = query.Search,
            MinPrice = query.MinPrice,
            MaxPrice = query.MaxPrice,
            CategoryId = query.CategoryId,
            BrandId = query.BrandId,
            InStock = query.InStock,
            SortBy = query.SortBy.ToLowerInvariant(),
            Descending = string.Equals(query.SortDir, "desc", StringComparison.OrdinalIgnoreCase)
        };

        var total = await uow.Products.CountAsync(new ProductsFilterSpec(filter), ct);                          // SELECT COUNT(*)
        var items = await uow.Products.ListAsync(new ProductsPageSpec(filter, (page - 1) * pageSize, pageSize), ct); // OFFSET/FETCH

        logger.LogInformation("Products listed: Page={Page} PageSize={PageSize} Total={TotalCount} SortBy={SortBy}",
            page, pageSize, total, filter.SortBy);
        return new PagedResult<ProductListItemDto>(mapper.Map<List<ProductListItemDto>>(items), total, page, pageSize);
    }

    public async Task<IReadOnlyList<ProductListItemDto>> GetLowStockAsync(int threshold, CancellationToken ct)
    {
        if (threshold < 0) throw new ValidationException("Threshold cannot be negative");
        // інша специфікація — той самий ListAsync, репозиторій не змінюється
        var items = await uow.Products.ListAsync(new LowStockProductsSpec(threshold), ct);
        return mapper.Map<List<ProductListItemDto>>(items);
    }

    public async Task<ProductDetailsDto> GetByIdAsync(long id, CancellationToken ct)
    {
        var product = await uow.Products.GetWithDetailsAsync(id, ct)                // Eager Loading: один запит
                      ?? throw new EntityNotFoundException($"Product {id} not found");
        return mapper.Map<ProductDetailsDto>(product);
    }

    public async Task<ProductDto> CreateAsync(CreateProductDto input, CancellationToken ct)
    {
        if (await uow.Brands.GetByIdAsync(input.BrandId, ct) is null)
            throw new ValidationException($"Brand {input.BrandId} does not exist");
        if (await uow.Products.ExistsBySkuAsync(input.Sku, ct))
            throw new ConflictException($"Product with SKU '{input.Sku}' already exists");

        var product = new Product(input.BrandId, input.Name, input.Sku, input.Price);   // інваріанти — у сутності
        if (input.InitialStock > 0) product.AddStock(input.InitialStock);

        await uow.Products.AddAsync(product, ct);
        await uow.SaveChangesAsync(ct);

        logger.LogInformation("Product {ProductId} created with SKU {Sku}", product.Id, product.Sku);
        return mapper.Map<ProductDto>(product);
    }

    public async Task<ProductDto> UpdateAsync(long id, UpdateProductDto input, CancellationToken ct)
    {
        var product = await FindAsync(id, ct);
        product.Rename(input.Name);
        product.SetActive(input.IsActive);
        await uow.SaveChangesAsync(ct);
        return mapper.Map<ProductDto>(product);
    }

    public async Task<ProductDto> ChangePriceAsync(long id, ChangePriceDto input, CancellationToken ct)
    {
        var product = await FindAsync(id, ct);
        var old = product.Price;
        product.ChangePrice(input.Price);
        await uow.SaveChangesAsync(ct);
        logger.LogInformation("Product {ProductId} price changed {OldPrice} -> {NewPrice}", id, old, product.Price);
        return mapper.Map<ProductDto>(product);
    }

    public async Task<ProductDto> AddStockAsync(long id, StockChangeDto input, CancellationToken ct)
    {
        var product = await FindAsync(id, ct);
        product.AddStock(input.Quantity);
        await uow.SaveChangesAsync(ct);
        return mapper.Map<ProductDto>(product);
    }

    public async Task<ProductDto> RemoveStockAsync(long id, StockChangeDto input, CancellationToken ct)
    {
        var product = await FindAsync(id, ct);
        product.RemoveStock(input.Quantity);                                          // DomainException, якщо не вистачає
        await uow.SaveChangesAsync(ct);                                               // RowVersion захищає від гонки
        logger.LogInformation("Product {ProductId} stock decreased by {Quantity}, left {AvailableStock}",
            id, input.Quantity, product.AvailableStock);
        return mapper.Map<ProductDto>(product);
    }

    public async Task<ProductDetailsDto> SaveDetailAsync(long id, SaveProductDetailDto input, CancellationToken ct)
    {
        var product = await GetTrackedWithDetailsAsync(id, ct);
        product.SetDetail(input.Description, input.WeightGrams, input.WarrantyMonths, input.Specs);
        await uow.SaveChangesAsync(ct);
        return mapper.Map<ProductDetailsDto>(product);
    }

    public async Task<ProductDetailsDto> AddImageAsync(long id, AddProductImageDto input, CancellationToken ct)
    {
        var product = await GetTrackedWithDetailsAsync(id, ct);
        product.AddImage(input.Url, input.SortOrder, input.IsMain);
        await uow.SaveChangesAsync(ct);
        return mapper.Map<ProductDetailsDto>(product);
    }

    public async Task AssignCategoryAsync(long productId, long categoryId, CancellationToken ct)
    {
        await FindAsync(productId, ct);
        if (await uow.Categories.GetByIdAsync(categoryId, ct) is null)
            throw new EntityNotFoundException($"Category {categoryId} not found");
        if (await uow.ProductCategories.FindAsync(productId, categoryId, ct) is not null)
            throw new ConflictException("Product is already in this category");

        await uow.ProductCategories.AddAsync(new ProductCategory(productId, categoryId), ct);   // AddedAt — власне поле M:N
        await uow.SaveChangesAsync(ct);
    }

    public async Task RemoveCategoryAsync(long productId, long categoryId, CancellationToken ct)
    {
        var link = await uow.ProductCategories.FindAsync(productId, categoryId, ct)
                   ?? throw new EntityNotFoundException($"Product {productId} is not in category {categoryId}");
        uow.ProductCategories.Delete(link);
        await uow.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct)
    {
        var product = await FindAsync(id, ct);
        uow.Products.Delete(product);                                                 // деталі, зображення й зв'язки — каскадом у БД
        await uow.SaveChangesAsync(ct);
        logger.LogInformation("Product {ProductId} deleted", id);
    }

    private async Task<Product> FindAsync(long id, CancellationToken ct) =>
        await uow.Products.GetByIdAsync(id, ct) ?? throw new EntityNotFoundException($"Product {id} not found");

    private async Task<Product> GetTrackedWithDetailsAsync(long id, CancellationToken ct) =>
        await uow.Products.GetWithDetailsAsync(id, ct) ?? throw new EntityNotFoundException($"Product {id} not found");
}
