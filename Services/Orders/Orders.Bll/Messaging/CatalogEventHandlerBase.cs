using Microsoft.Extensions.Logging;
using Orders.Dal.Interfaces;
using Platform.Messaging.Consuming;
using Platform.Messaging.Events;

namespace Orders.Bll.Messaging;

/// <summary>
/// Шаблон ідемпотентної обробки події Каталогу в Orders (Лаб. 08). Усе в ОДНІЙ транзакції Unit of Work:
/// 1) <c>ProcessedEvents.TryAddAsync(EventId)</c> — унікальний ключ; дубль → відкат без змін, результат Duplicate;
/// 2) зміна локальної копії товару (<see cref="ApplyAsync"/>);
/// 3) Commit — запис про обробку й бізнес-зміна фіксуються атомарно. Збій на будь-якому кроці → Rollback і виняток:
///    consumer зробить повтор (x-retry-count) або відправить повідомлення в DLQ. Запис про обробку відкотиться разом зі зміною,
///    тож повтор не буде помилково сприйнятий дублем.
/// Окремий клас-обробник на кожен тип події успадковує цей шаблон і реалізує лише власну бізнес-зміну.
/// </summary>
public abstract class CatalogEventHandlerBase<TEvent>(IUnitOfWork uow, ILogger logger) : IEventHandler<TEvent>
    where TEvent : IntegrationEvent
{
    public async Task<HandlingResult> HandleAsync(TEvent @event, CancellationToken ct)
    {
        await uow.BeginTransactionAsync(ct: ct);
        try
        {
            if (!await uow.ProcessedEvents.TryAddAsync(@event.EventId, typeof(TEvent).Name, ct))
            {
                await uow.RollbackAsync(ct);
                logger.LogInformation("Duplicate {EventType} {EventId} (CorrelationId {CorrelationId}): already processed, skipped",
                    typeof(TEvent).Name, @event.EventId, @event.CorrelationId);
                return HandlingResult.Duplicate;
            }

            var result = await ApplyAsync(uow, @event, ct);
            await uow.CommitAsync(ct);
            return result;
        }
        catch
        {
            await uow.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    /// <summary>Бізнес-зміна локальної копії; викликається всередині відкритої транзакції.</summary>
    protected abstract Task<HandlingResult> ApplyAsync(IUnitOfWork uow, TEvent @event, CancellationToken ct);
}

/// <summary>Тимчасовий збій: товар ще не реплікований (подія прийшла раніше за product.created). Повтор після паузи зазвичай допомагає.</summary>
public sealed class ProductNotReplicatedException(long productId)
    : Exception($"Product {productId} is not replicated to Orders yet");
