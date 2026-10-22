using AutoMapper;
using Microsoft.Extensions.Logging;
using Orders.Bll.Dtos;
using Orders.Dal.Interfaces;
using Orders.Domain.Exceptions;
using Orders.Domain.Models;

namespace Orders.Bll.Services;

public sealed class ProductService(IUnitOfWork uow, IMapper mapper, ILogger<ProductService> logger) : IProductService
{
    public async Task<IReadOnlyList<ProductDto>> GetAllAsync(CancellationToken ct) =>
        mapper.Map<List<ProductDto>>(await uow.Products.GetAllAsync(ct));

    public async Task<ProductDto> GetByIdAsync(long id, CancellationToken ct) =>
        mapper.Map<ProductDto>(await uow.Products.GetByIdAsync(id, ct)
                               ?? throw new NotFoundException($"Product {id} not found"));

    public async Task<ProductDto> SyncFromCatalogAsync(long id, SyncProductDto dto, CancellationToken ct)
    {
        if (id < 1) throw new ValidationException("Product id must be positive");

        await uow.Products.UpsertFromCatalogAsync(new Product
        {
            Id = id, Name = dto.Name.Trim(), Price = dto.Price, IsActive = dto.IsActive, Stock = dto.Stock
        }, ct);
        logger.LogInformation("Product {ProductId} synchronized from Catalog", id);
        return await GetByIdAsync(id, ct);
    }
}
