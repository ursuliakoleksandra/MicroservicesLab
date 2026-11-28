namespace Orders.Dal.Interfaces;

/// <summary>Журнал оброблених інтеграційних подій (Лаб. 08): основа ідемпотентності споживача.</summary>
public interface IProcessedEventRepository
{
    /// <summary>
    /// Пробує зафіксувати EventId у таблиці dbo.ProcessedEvents у ПОТОЧНІЙ транзакції Unit of Work.
    /// true — подію бачимо вперше; false — EventId уже є (помилка унікального ключа 2627/2601) → подію вже оброблено.
    /// </summary>
    Task<bool> TryAddAsync(Guid eventId, string eventType, CancellationToken ct = default);
}
