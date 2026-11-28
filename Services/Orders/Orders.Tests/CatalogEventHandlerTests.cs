using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Orders.Bll.Messaging;
using Orders.Dal.Interfaces;
using Platform.Messaging.Consuming;
using Platform.Messaging.Events;
using Xunit;

namespace Orders.Tests;

/// <summary>Лаб. 08: обробники подій Каталогу в Orders. UoW замінено заглушкою; перевіряються транзакція й ідемпотентність.</summary>
public class CatalogEventHandlerTests
{
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IProductRepository _products = Substitute.For<IProductRepository>();
    private readonly IProcessedEventRepository _processed = Substitute.For<IProcessedEventRepository>();

    public CatalogEventHandlerTests()
    {
        _uow.Products.Returns(_products);
        _uow.ProcessedEvents.Returns(_processed);
    }

    private ProductPriceChangedHandler PriceHandler() => new(_uow, NullLogger<ProductPriceChangedHandler>.Instance);

    [Fact]
    public async Task PriceChanged_FirstDelivery_AppliesChange_AndCommitsInOneTransaction()
    {
        var e = new ProductPriceChangedEvent(5, "Laptop", 10m, 12m);
        _processed.TryAddAsync(e.EventId, nameof(ProductPriceChangedEvent), Arg.Any<CancellationToken>()).Returns(true);
        _products.ApplyCatalogChangeAsync(5, null, 12m, null, e.OccurredAt, Arg.Any<CancellationToken>()).Returns(true);

        var result = await PriceHandler().HandleAsync(e, CancellationToken.None);

        Assert.Equal(HandlingResult.Processed, result);
        Received.InOrder(() =>
        {
            _uow.BeginTransactionAsync(Arg.Any<System.Data.IsolationLevel>(), Arg.Any<CancellationToken>());
            _processed.TryAddAsync(e.EventId, nameof(ProductPriceChangedEvent), Arg.Any<CancellationToken>());
            _products.ApplyCatalogChangeAsync(5, null, 12m, null, e.OccurredAt, Arg.Any<CancellationToken>());
            _uow.CommitAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task PriceChanged_Duplicate_DoesNotChangeData_AndRollsBack()
    {
        var e = new ProductPriceChangedEvent(5, "Laptop", 10m, 12m);
        _processed.TryAddAsync(e.EventId, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await PriceHandler().HandleAsync(e, CancellationToken.None);

        Assert.Equal(HandlingResult.Duplicate, result);
        await _products.DidNotReceiveWithAnyArgs().ApplyCatalogChangeAsync(default, default, default, default, default, default);
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
        await _uow.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PriceChanged_StorageFails_RollsBack_AndRethrowsForRetry()
    {
        var e = new ProductPriceChangedEvent(5, "Laptop", 10m, 12m);
        _processed.TryAddAsync(e.EventId, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _products.ApplyCatalogChangeAsync(default, default, default, default, default, default)
            .ReturnsForAnyArgs<bool>(_ => throw new TimeoutException("db timeout"));

        await Assert.ThrowsAsync<TimeoutException>(() => PriceHandler().HandleAsync(e, CancellationToken.None));

        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
        await _uow.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PriceChanged_ProductMissing_CreatesLocalCopyFromEvent()
    {
        var e = new ProductPriceChangedEvent(9, "Phone", 1m, 2m);
        _processed.TryAddAsync(e.EventId, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _products.ApplyCatalogChangeAsync(9, null, 2m, null, e.OccurredAt, Arg.Any<CancellationToken>()).Returns(false);

        await PriceHandler().HandleAsync(e, CancellationToken.None);

        await _products.Received(1).InsertFromCatalogAsync(9, "Phone", 2m, true, 0, e.OccurredAt, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Renamed_ProductNotReplicatedYet_ThrowsTransientError()
    {
        var e = new ProductRenamedEvent(9, "A", "B");
        _processed.TryAddAsync(e.EventId, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _products.ApplyCatalogChangeAsync(9, "B", null, null, e.OccurredAt, Arg.Any<CancellationToken>()).Returns(false);

        var sut = new ProductRenamedHandler(_uow, NullLogger<ProductRenamedHandler>.Instance);

        await Assert.ThrowsAsync<ProductNotReplicatedException>(() => sut.HandleAsync(e, CancellationToken.None));
        await _uow.Received(1).RollbackAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StockReplenished_NonPositiveQuantity_IsPermanentFailure()
    {
        var e = new ProductStockReplenishedEvent(1, "Laptop", 0);
        _processed.TryAddAsync(e.EventId, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        var sut = new ProductStockReplenishedHandler(_uow, NullLogger<ProductStockReplenishedHandler>.Instance);

        await Assert.ThrowsAsync<PermanentFailureException>(() => sut.HandleAsync(e, CancellationToken.None));
    }

    [Fact]
    public async Task Deleted_UnknownProduct_IsSkipped()
    {
        var e = new ProductDeletedEvent(77, "Ghost");
        _processed.TryAddAsync(e.EventId, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _products.ApplyCatalogChangeAsync(77, null, null, false, e.OccurredAt, Arg.Any<CancellationToken>()).Returns(false);

        var sut = new ProductDeletedHandler(_uow, NullLogger<ProductDeletedHandler>.Instance);

        Assert.Equal(HandlingResult.Skipped, await sut.HandleAsync(e, CancellationToken.None));
    }
}
