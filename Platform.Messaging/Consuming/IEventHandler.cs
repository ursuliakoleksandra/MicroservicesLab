using Platform.Messaging.Events;

namespace Platform.Messaging.Consuming;

/// <summary>Результат обробки події (для логів і метрик). Помилки сигналізуються винятками.</summary>
public enum HandlingResult
{
    Processed,
    /// <summary>EventId уже є в таблиці оброблених — стан не змінювався (ідемпотентність).</summary>
    Duplicate,
    /// <summary>Подію коректно отримано, але застосовувати нічого (застаріла за часом, немає локальної копії).</summary>
    Skipped
}

/// <summary>Обробник одного типу події; створюється в scope повідомлення (залежності — через DI).</summary>
public interface IEventHandler<in TEvent> where TEvent : IntegrationEvent
{
    Task<HandlingResult> HandleAsync(TEvent @event, CancellationToken ct);
}

/// <summary>Постійний збій: повтор не допоможе (несумісна версія схеми, порушення бізнес-інваріанту) → одразу в DLQ.</summary>
public sealed class PermanentFailureException(string message, Exception? inner = null) : Exception(message, inner);
