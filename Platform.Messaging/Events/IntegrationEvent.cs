using Platform.ServiceDefaults.Correlation;

namespace Platform.Messaging.Events;

/// <summary>
/// Базовий тип інтеграційних подій. Події незмінні (record, init-властивості) і самодостатні: споживач не повинен
/// звертатися до сервісу-джерела по додаткові дані. EventId — для ідемпотентності, CorrelationId — для трасування.
/// </summary>
public abstract record IntegrationEvent
{
    /// <summary>Поточна версія схеми: нові поля лише додаються, наявні не перейменовуються й не видаляються.</summary>
    public const int CurrentSchemaVersion = 1;

    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;

    /// <summary>За замовчуванням — CorrelationId поточного запиту/повідомлення (див. CorrelationContext).</summary>
    public string? CorrelationId { get; init; } = CorrelationContext.Current;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
}
