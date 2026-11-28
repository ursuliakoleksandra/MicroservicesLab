namespace Platform.Messaging.Events;

// Події Каталогу (Catalog Service — джерело істини для даних про товари). Імена — в минулому часі: це факти, що вже відбулися.
// Події самодостатні: містять усе потрібне споживачам (ім'я, нову ціну), щоб не робити зворотних запитів до Каталогу.

/// <summary>Створено товар. Routing key: <see cref="CatalogRoutingKeys.Created"/>.</summary>
public sealed record ProductCreatedEvent(long ProductId, string Name, decimal Price, bool IsActive, int InitialStock) : IntegrationEvent;

/// <summary>Змінилася ціна товару. Routing key: <see cref="CatalogRoutingKeys.PriceChanged"/>.</summary>
public sealed record ProductPriceChangedEvent(long ProductId, string ProductName, decimal OldPrice, decimal NewPrice) : IntegrationEvent;

/// <summary>Товар перейменовано. Routing key: <see cref="CatalogRoutingKeys.Renamed"/>.</summary>
public sealed record ProductRenamedEvent(long ProductId, string OldName, string NewName) : IntegrationEvent;

/// <summary>Змінилася активність (доступність до замовлення). Routing key: <see cref="CatalogRoutingKeys.ActivityChanged"/>.</summary>
public sealed record ProductActivityChangedEvent(long ProductId, string ProductName, bool IsActive) : IntegrationEvent;

/// <summary>Поповнено склад (надходження товару). Routing key: <see cref="CatalogRoutingKeys.StockReplenished"/>.</summary>
public sealed record ProductStockReplenishedEvent(long ProductId, string ProductName, int Quantity) : IntegrationEvent;

/// <summary>Товар видалено з каталогу. Routing key: <see cref="CatalogRoutingKeys.Deleted"/>.</summary>
public sealed record ProductDeletedEvent(long ProductId, string ProductName) : IntegrationEvent;

public static class CatalogRoutingKeys
{
    public const string Created = "product.created";
    public const string PriceChanged = "product.price_changed";
    public const string Renamed = "product.renamed";
    public const string ActivityChanged = "product.activity_changed";
    public const string StockReplenished = "product.stock_replenished";
    public const string Deleted = "product.deleted";
}
