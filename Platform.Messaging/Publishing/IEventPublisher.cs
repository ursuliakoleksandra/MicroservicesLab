using Platform.Messaging.Events;

namespace Platform.Messaging.Publishing;

public interface IEventPublisher
{
    /// <summary>Публікує подію в exchange з routing key. Викликається ЛИШЕ після успішного збереження змін у БД.</summary>
    Task PublishAsync<T>(T @event, string exchange, string routingKey, CancellationToken ct) where T : IntegrationEvent;
}
