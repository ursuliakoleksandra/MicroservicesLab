using Microsoft.Extensions.Logging;
using Orders.Dal.Interfaces;
using Platform.Messaging.Consuming;
using Platform.Messaging.Events;

namespace Orders.Bll.Messaging;

// Політика дублювання (docs/duplication-policy.md): OrdersDb.Products.Name / Price / IsActive оновлюються подіями Каталогу.
// Знімки в OrderItems (ProductName, UnitPrice) НЕ чіпаються — вже оформлене замовлення зберігає ціну на момент покупки.

/// <summary>product.created → додає локальну копію (якщо її ще немає); залишок = початковий залишок з події.</summary>
public sealed class ProductCreatedHandler(IUnitOfWork uow, ILogger<ProductCreatedHandler> logger)
    : CatalogEventHandlerBase<ProductCreatedEvent>(uow, logger)
{
    private readonly ILogger _logger = logger;

    protected override async Task<HandlingResult> ApplyAsync(IUnitOfWork uow, ProductCreatedEvent e, CancellationToken ct)
    {
        var inserted = await uow.Products.InsertFromCatalogAsync(e.ProductId, e.Name, e.Price, e.IsActive, e.InitialStock, e.OccurredAt, ct);
        if (!inserted)
            // рядок міг з'явитися раніше (подія price_changed прийшла першою): доповнюємо поля з версійним захистом
            await uow.Products.ApplyCatalogChangeAsync(e.ProductId, e.Name, e.Price, e.IsActive, e.OccurredAt, ct);

        _logger.LogInformation("Product {ProductId} replicated from Catalog (created={Inserted}, CorrelationId {CorrelationId})",
            e.ProductId, inserted, e.CorrelationId);
        return HandlingResult.Processed;
    }
}

/// <summary>product.price_changed → оновлює Products.Price (лише якщо подія новіша за вже застосовану).</summary>
public sealed class ProductPriceChangedHandler(IUnitOfWork uow, ILogger<ProductPriceChangedHandler> logger)
    : CatalogEventHandlerBase<ProductPriceChangedEvent>(uow, logger)
{
    private readonly ILogger _logger = logger;

    protected override async Task<HandlingResult> ApplyAsync(IUnitOfWork uow, ProductPriceChangedEvent e, CancellationToken ct)
    {
        if (!await uow.Products.ApplyCatalogChangeAsync(e.ProductId, null, e.NewPrice, null, e.OccurredAt, ct))
        {
            // події самодостатні: товару ще немає — створюємо його з даних цієї події (назва + нова ціна)
            await uow.Products.InsertFromCatalogAsync(e.ProductId, e.ProductName, e.NewPrice, true, 0, e.OccurredAt, ct);
        }

        _logger.LogInformation("Product {ProductId} price replicated: {OldPrice} -> {NewPrice} (CorrelationId {CorrelationId})",
            e.ProductId, e.OldPrice, e.NewPrice, e.CorrelationId);
        return HandlingResult.Processed;
    }
}

/// <summary>product.renamed → оновлює Products.Name. Якщо товару ще немає — тимчасовий збій (повторимо, дочекавшись product.created).</summary>
public sealed class ProductRenamedHandler(IUnitOfWork uow, ILogger<ProductRenamedHandler> logger)
    : CatalogEventHandlerBase<ProductRenamedEvent>(uow, logger)
{
    private readonly ILogger _logger = logger;

    protected override async Task<HandlingResult> ApplyAsync(IUnitOfWork uow, ProductRenamedEvent e, CancellationToken ct)
    {
        if (!await uow.Products.ApplyCatalogChangeAsync(e.ProductId, e.NewName, null, null, e.OccurredAt, ct))
            throw new ProductNotReplicatedException(e.ProductId);

        _logger.LogInformation("Product {ProductId} name replicated: '{OldName}' -> '{NewName}' (CorrelationId {CorrelationId})",
            e.ProductId, e.OldName, e.NewName, e.CorrelationId);
        return HandlingResult.Processed;
    }
}

/// <summary>product.activity_changed → оновлює Products.IsActive.</summary>
public sealed class ProductActivityChangedHandler(IUnitOfWork uow, ILogger<ProductActivityChangedHandler> logger)
    : CatalogEventHandlerBase<ProductActivityChangedEvent>(uow, logger)
{
    private readonly ILogger _logger = logger;

    protected override async Task<HandlingResult> ApplyAsync(IUnitOfWork uow, ProductActivityChangedEvent e, CancellationToken ct)
    {
        if (!await uow.Products.ApplyCatalogChangeAsync(e.ProductId, null, null, e.IsActive, e.OccurredAt, ct))
            throw new ProductNotReplicatedException(e.ProductId);

        _logger.LogInformation("Product {ProductId} activity replicated: IsActive={IsActive} (CorrelationId {CorrelationId})",
            e.ProductId, e.IsActive, e.CorrelationId);
        return HandlingResult.Processed;
    }
}

/// <summary>product.stock_replenished → збільшує Products.Stock (залишок, яким володіє Orders; поповнення приходить подіями).</summary>
public sealed class ProductStockReplenishedHandler(IUnitOfWork uow, ILogger<ProductStockReplenishedHandler> logger)
    : CatalogEventHandlerBase<ProductStockReplenishedEvent>(uow, logger)
{
    private readonly ILogger _logger = logger;

    protected override async Task<HandlingResult> ApplyAsync(IUnitOfWork uow, ProductStockReplenishedEvent e, CancellationToken ct)
    {
        if (e.Quantity <= 0)
            throw new Platform.Messaging.Consuming.PermanentFailureException($"Invalid replenishment quantity {e.Quantity} for product {e.ProductId}");

        // Стан «+N» не ідемпотентний сам по собі, тому важливий ProcessedEvents: повторна доставка не додасть залишок вдруге.
        try { await uow.Products.IncreaseStockAsync(e.ProductId, e.Quantity, ct); }
        catch (Orders.Domain.Exceptions.NotFoundException) { throw new ProductNotReplicatedException(e.ProductId); }

        _logger.LogInformation("Product {ProductId} stock replenished by {Quantity} (CorrelationId {CorrelationId})",
            e.ProductId, e.Quantity, e.CorrelationId);
        return HandlingResult.Processed;
    }
}

/// <summary>
/// product.deleted → товар недоступний для нових замовлень (IsActive = false). Рядок не видаляється: на нього посилаються
/// OrderItems (FOREIGN KEY), а історія замовлень має зберегтися.
/// </summary>
public sealed class ProductDeletedHandler(IUnitOfWork uow, ILogger<ProductDeletedHandler> logger)
    : CatalogEventHandlerBase<ProductDeletedEvent>(uow, logger)
{
    private readonly ILogger _logger = logger;

    protected override async Task<HandlingResult> ApplyAsync(IUnitOfWork uow, ProductDeletedEvent e, CancellationToken ct)
    {
        var found = await uow.Products.ApplyCatalogChangeAsync(e.ProductId, null, null, false, e.OccurredAt, ct);
        _logger.LogInformation("Product {ProductId} deleted in Catalog; local copy deactivated={Found} (CorrelationId {CorrelationId})",
            e.ProductId, found, e.CorrelationId);
        return found ? HandlingResult.Processed : HandlingResult.Skipped;
    }
}
