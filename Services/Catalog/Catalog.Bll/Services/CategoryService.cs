using AutoMapper;
using Catalog.Bll.Caching;
using Catalog.Bll.Dtos;
using Catalog.Dal.Interfaces;
using Catalog.Domain.Entities;
using Catalog.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Platform.Caching;

namespace Catalog.Bll.Services;

public sealed class CategoryService(IUnitOfWork uow, IMapper mapper, ILogger<CategoryService> logger, ITwoLevelCache cache)
    : ICategoryService
{
    // довідник: читають постійно, змінюється рідко -> довгий absolute TTL
    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken ct) =>
        (await cache.GetOrCreateAsync<IReadOnlyList<CategoryDto>>(CatalogCacheKeys.Categories,
            async innerCt => mapper.Map<List<CategoryDto>>(await uow.Categories.GetAllAsync(innerCt)),
            CatalogCachePolicies.Reference, ct))!;

    public async Task<CategoryDto> GetByIdAsync(long id, CancellationToken ct) =>
        mapper.Map<CategoryDto>(await uow.Categories.GetByIdAsync(id, ct)
                                ?? throw new EntityNotFoundException($"Category {id} not found"));

    public async Task<CategoryWithProductsDto> GetWithProductsAsync(long id, CancellationToken ct)
    {
        var category = await uow.Categories.GetWithProductsAsync(id, ct)            // Explicit Loading: 2 запити
                       ?? throw new EntityNotFoundException($"Category {id} not found");
        return mapper.Map<CategoryWithProductsDto>(category);
    }

    public async Task<IReadOnlyList<ProductDto>> GetRecentlyAddedProductsAsync(string categoryName, DateTime? since, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(categoryName)) throw new ValidationException("Category name is required");
        if (await uow.Categories.GetByNameAsync(categoryName, ct) is null)
            throw new EntityNotFoundException($"Category '{categoryName}' not found");

        var from = since ?? DateTime.UtcNow.AddDays(-30);
        var products = await uow.ProductCategories.GetRecentlyAddedAsync(categoryName, from, ct);   // LINQ M:N
        return mapper.Map<List<ProductDto>>(products);
    }

    public async Task<CategoryDto> CreateAsync(CreateCategoryDto input, CancellationToken ct)
    {
        var category = new Category(input.Name, input.Slug);
        if (await uow.Categories.ExistsByNameOrSlugAsync(category.Name, category.Slug, ct))
            throw new ConflictException("Category with the same name or slug already exists");

        await uow.Categories.AddAsync(category, ct);
        await uow.SaveChangesAsync(ct);
        await cache.RemoveAsync(CatalogCacheKeys.Categories, "CategoryCreated", ct);
        logger.LogInformation("Category {CategoryId} created", category.Id);
        return mapper.Map<CategoryDto>(category);
    }
}

public sealed class BrandService(IUnitOfWork uow, IMapper mapper, ITwoLevelCache cache) : IBrandService
{
    public async Task<IReadOnlyList<BrandDto>> GetAllAsync(CancellationToken ct) =>
        (await cache.GetOrCreateAsync<IReadOnlyList<BrandDto>>(CatalogCacheKeys.Brands,
            async innerCt => mapper.Map<List<BrandDto>>(await uow.Brands.GetAllAsync(innerCt)),
            CatalogCachePolicies.Reference, ct))!;

    public async Task<BrandDto> CreateAsync(CreateBrandDto input, CancellationToken ct)
    {
        var brand = new Brand(input.Name, input.Country);
        if (await uow.Brands.ExistsByNameAsync(brand.Name, ct))
            throw new ConflictException($"Brand '{brand.Name}' already exists");

        await uow.Brands.AddAsync(brand, ct);
        await uow.SaveChangesAsync(ct);
        await cache.RemoveAsync(CatalogCacheKeys.Brands, "BrandCreated", ct);
        return mapper.Map<BrandDto>(brand);
    }
}
