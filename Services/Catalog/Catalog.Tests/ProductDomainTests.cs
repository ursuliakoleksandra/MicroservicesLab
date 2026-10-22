using Catalog.Domain.Entities;
using Catalog.Domain.Exceptions;
using Xunit;

namespace Catalog.Tests;

/// <summary>Бізнес-правила живуть у сутності й перевіряються без БД.</summary>
public class ProductDomainTests
{
    private static Product NewProduct(decimal price = 100m) => new(1, "Laptop", "LAP-1", price);

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Constructor_NonPositivePrice_Throws(decimal price)
        => Assert.Throws<DomainException>(() => NewProduct(price));

    [Fact]
    public void Constructor_InvalidSku_Throws()
        => Assert.Throws<DomainException>(() => new Product(1, "Laptop", "bad sku!", 10m));

    [Fact]
    public void RemoveStock_MoreThanAvailable_Throws_AndKeepsStock()
    {
        var product = NewProduct();
        product.AddStock(3);

        Assert.Throws<DomainException>(() => product.RemoveStock(5));
        Assert.Equal(3, product.AvailableStock);
    }

    [Fact]
    public void AddAndRemoveStock_ChangesStock()
    {
        var product = NewProduct();
        product.AddStock(10);
        product.RemoveStock(4);
        Assert.Equal(6, product.AvailableStock);
    }

    [Fact]
    public void ChangePrice_Invalid_Throws_AndKeepsPrice()
    {
        var product = NewProduct(50m);
        Assert.Throws<DomainException>(() => product.ChangePrice(-1m));
        Assert.Equal(50m, product.Price);
    }

    [Fact]
    public void AddImage_SecondMainImage_Throws()
    {
        var product = NewProduct();
        product.AddImage("/a.jpg", 0, isMain: true);
        Assert.Throws<DomainException>(() => product.AddImage("/b.jpg", 1, isMain: true));
    }
}
