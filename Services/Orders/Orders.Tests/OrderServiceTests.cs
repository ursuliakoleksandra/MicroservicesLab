using System.Data;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Orders.Bll.Dtos;
using Orders.Bll.Mapping;
using Orders.Bll.Services;
using Orders.Dal.Interfaces;
using Orders.Domain.Exceptions;
using Orders.Domain.Models;
using Xunit;

namespace Orders.Tests;

/// <summary>Unit-тести BLL: DAL/UoW замінено заглушками (NSubstitute). SQL і база не потрібні.</summary>
public class OrderServiceTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IOrderRepository _orders = Substitute.For<IOrderRepository>();
    private readonly IProductRepository _products = Substitute.For<IProductRepository>();
    private readonly ICustomerRepository _customers = Substitute.For<ICustomerRepository>();
    private readonly OrderService _sut;

    public OrderServiceTests()
    {
        _uow.Orders.Returns(_orders);
        _uow.Products.Returns(_products);
        _uow.Customers.Returns(_customers);
        _sut = new OrderService(_uow, CreateMapper(), NullLogger<OrderService>.Instance);
    }

    private static IMapper CreateMapper()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAutoMapper(cfg => cfg.AddMaps(typeof(OrderProfile).Assembly));
        return services.BuildServiceProvider().GetRequiredService<IMapper>();
    }

    private static Order OrderWith(string status, params (long productId, int units)[] lines) => new()
    {
        Id = 1, CustomerId = 1, Status = status, RowVersion = new byte[8],
        Items = lines.Select(l => new OrderItem { OrderId = 1, ProductId = l.productId, ProductName = "P", UnitPrice = 10, Units = l.units }).ToList()
    };

    [Fact]
    public void MappingConfiguration_IsValid()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAutoMapper(cfg => cfg.AddMaps(typeof(OrderProfile).Assembly));
        services.BuildServiceProvider().GetRequiredService<IConfigurationProvider>().AssertConfigurationIsValid();
    }

    [Fact]
    public async Task Confirm_NonExistentOrder_ThrowsNotFound_AndRollsBack()
    {
        _orders.GetWithItemsAsync(99, Arg.Any<CancellationToken>()).Returns((Order?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _sut.ConfirmOrderAsync(99, CancellationToken.None));

        await _uow.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Confirm_HappyPath_ChangesStatusDecreasesStockAndCommits()
    {
        _orders.GetWithItemsAsync(1, Arg.Any<CancellationToken>()).Returns(OrderWith(OrderStatus.Submitted, (2, 3), (1, 1)));

        await _sut.ConfirmOrderAsync(1, CancellationToken.None);

        await _orders.Received(1).ChangeStatusAsync(1, OrderStatus.AwaitingValidation, Arg.Any<string?>(), null, Arg.Any<CancellationToken>());
        await _orders.Received(1).ChangeStatusAsync(1, OrderStatus.StockConfirmed, Arg.Any<string?>(), null, Arg.Any<CancellationToken>());
        await _products.Received(1).DecreaseStockAsync(1, 1, Arg.Any<CancellationToken>());
        await _products.Received(1).DecreaseStockAsync(2, 3, Arg.Any<CancellationToken>());
        await _uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        await _uow.DidNotReceive().RollbackAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Confirm_InsufficientStock_RollsBackAndDoesNotCommit()
    {
        _orders.GetWithItemsAsync(1, Arg.Any<CancellationToken>()).Returns(OrderWith(OrderStatus.Submitted, (1, 100)));
        _products.DecreaseStockAsync(1, 100, Arg.Any<CancellationToken>())
                 .Returns(Task.FromException(new BusinessConflictException("Insufficient stock for product 1")));

        await Assert.ThrowsAsync<BusinessConflictException>(() => _sut.ConfirmOrderAsync(1, CancellationToken.None));

        await _uow.Received(1).RollbackAsync(Arg.Any<CancellationToken>());   // зміни першого репозиторію відкочено
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_UnknownCustomer_ThrowsValidation_AndRollsBack()
    {
        _customers.GetByIdAsync(5, Arg.Any<CancellationToken>()).Returns((Customer?)null);
        var dto = new CreateOrderDto { CustomerId = 5, ShippingAddress = "Kyiv, 1", Items = [new() { ProductId = 1, Units = 1 }] };

        await Assert.ThrowsAsync<ValidationException>(() => _sut.CreateAsync(dto, CancellationToken.None));

        await _uow.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
        await _orders.DidNotReceive().AddAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_SnapshotsProductNameAndPrice()
    {
        _customers.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(new Customer { Id = 1 });
        _products.GetByIdsAsync(Arg.Any<IReadOnlyCollection<long>>(), Arg.Any<CancellationToken>())
                 .Returns(new List<Product> { new() { Id = 7, Name = "Laptop", Price = 999.50m, IsActive = true, Stock = 5 } });
        _orders.AddAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>()).Returns(1L);
        _orders.GetWithItemsAsync(1, Arg.Any<CancellationToken>()).Returns(OrderWith(OrderStatus.Submitted, (7, 2)));

        IEnumerable<OrderItem>? captured = null;
        await _orders.AddItemsAsync(Arg.Do<IEnumerable<OrderItem>>(items => captured = items.ToList()), Arg.Any<CancellationToken>());

        await _sut.CreateAsync(new CreateOrderDto { CustomerId = 1, ShippingAddress = "Lviv, 2", Items = [new() { ProductId = 7, Units = 2 }] },
            CancellationToken.None);

        var line = Assert.Single(captured!);
        Assert.Equal("Laptop", line.ProductName);      // знімок назви
        Assert.Equal(999.50m, line.UnitPrice);         // знімок ціни
        await _uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cancel_ConfirmedOrder_RestoresStock()
    {
        _orders.GetWithItemsAsync(1, Arg.Any<CancellationToken>()).Returns(OrderWith(OrderStatus.StockConfirmed, (3, 2)));

        await _sut.CancelOrderAsync(1, CancellationToken.None);

        await _products.Received(1).IncreaseStockAsync(3, 2, Arg.Any<CancellationToken>());
        await _uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }
}
