using AutoMapper;
using Catalog.Bll.Dtos;
using Catalog.Bll.Mapping;
using Catalog.Bll.Services;
using Catalog.Bll.Validators;
using Catalog.Dal.Interfaces;
using Catalog.Domain.Entities;
using Catalog.Domain.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Catalog.Tests;

/// <summary>Unit-тести BLL: UoW і репозиторії замінено заглушками; БД не потрібна.</summary>
public class ProductServiceTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IProductRepository _products = Substitute.For<IProductRepository>();
    private readonly IBrandRepository _brands = Substitute.For<IBrandRepository>();
    private readonly ProductService _sut;

    public ProductServiceTests()
    {
        _uow.Products.Returns(_products);
        _uow.Brands.Returns(_brands);
        _sut = new ProductService(_uow, CreateMapper(), NullLogger<ProductService>.Instance);
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAutoMapper(cfg => cfg.AddMaps(typeof(ProductProfile).Assembly));
        return services.BuildServiceProvider();
    }

    private static IMapper CreateMapper() => BuildProvider().GetRequiredService<IMapper>();

    [Fact]
    public void MappingConfiguration_IsValid()
        => BuildProvider().GetRequiredService<IConfigurationProvider>().AssertConfigurationIsValid();

    [Fact]
    public async Task Create_DuplicateSku_ThrowsConflict_AndDoesNotSave()
    {
        _brands.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(new Brand("Nova"));
        _products.ExistsBySkuAsync("LAP-1", Arg.Any<CancellationToken>()).Returns(true);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _sut.CreateAsync(new CreateProductDto { BrandId = 1, Name = "Laptop", Sku = "LAP-1", Price = 10m }, CancellationToken.None));

        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_UnknownBrand_ThrowsValidation()
    {
        _brands.GetByIdAsync(99, Arg.Any<CancellationToken>()).Returns((Brand?)null);

        await Assert.ThrowsAsync<ValidationException>(() =>
            _sut.CreateAsync(new CreateProductDto { BrandId = 99, Name = "Laptop", Sku = "LAP-1", Price = 10m }, CancellationToken.None));
    }

    [Fact]
    public async Task Create_Valid_AddsProduct_AndSavesOnce()
    {
        _brands.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(new Brand("Nova"));
        _products.ExistsBySkuAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        var dto = await _sut.CreateAsync(
            new CreateProductDto { BrandId = 1, Name = "Laptop", Sku = "LAP-1", Price = 999.99m, InitialStock = 7 }, CancellationToken.None);

        Assert.Equal("LAP-1", dto.Sku);
        Assert.Equal(7, dto.AvailableStock);
        await _products.Received(1).AddAsync(Arg.Any<Product>(), Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());   // один SaveChanges наприкінці операції
    }

    [Fact]
    public async Task GetById_Missing_ThrowsNotFound()
    {
        _products.GetWithDetailsAsync(42, Arg.Any<CancellationToken>()).Returns((Product?)null);
        await Assert.ThrowsAsync<EntityNotFoundException>(() => _sut.GetByIdAsync(42, CancellationToken.None));
    }

    [Fact]
    public async Task RemoveStock_MoreThanAvailable_ThrowsDomainException_AndDoesNotSave()
    {
        var product = new Product(1, "Laptop", "LAP-1", 10m);
        product.AddStock(2);
        _products.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(product);

        await Assert.ThrowsAsync<DomainException>(() =>
            _sut.RemoveStockAsync(1, new StockChangeDto { Quantity = 5 }, CancellationToken.None));

        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}

public class ValidatorTests
{
    [Fact]
    public async Task CreateProduct_InvalidSkuAndPrice_ReportsErrorsPerField()
    {
        var result = await new CreateProductDtoValidator().ValidateAsync(
            new CreateProductDto { BrandId = 1, Name = "X", Sku = "bad sku", Price = -1m });

        Assert.False(result.IsValid);
        var fields = result.ToDictionary().Keys;
        Assert.Contains("Sku", fields);
        Assert.Contains("Price", fields);
    }

    [Fact]
    public async Task ProductQuery_UnknownSortField_IsRejected()
    {
        var result = await new ProductQueryValidator().ValidateAsync(new ProductQuery { SortBy = "password" });
        Assert.False(result.IsValid);
    }
}
