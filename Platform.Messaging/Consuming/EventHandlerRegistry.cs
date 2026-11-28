using Microsoft.Extensions.DependencyInjection;
using Platform.Messaging.Events;

namespace Platform.Messaging.Consuming;

/// <summary>Відповідність «тип події (BasicProperties.Type) → обробник». Заповнюється при реєстрації споживача.</summary>
public sealed class EventHandlerRegistry
{
    internal sealed record Entry(Type EventType, Func<IServiceProvider, IntegrationEvent, CancellationToken, Task<HandlingResult>> Invoke);

    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    internal IServiceCollection Services { get; }
    internal EventHandlerRegistry(IServiceCollection services) => Services = services;

    /// <summary>Підписує окремий клас-обробник на тип події (обробник реєструється як scoped).</summary>
    public EventHandlerRegistry Handle<TEvent, THandler>()
        where TEvent : IntegrationEvent
        where THandler : class, IEventHandler<TEvent>
    {
        Services.AddScoped<THandler>();
        _entries[typeof(TEvent).Name] = new Entry(typeof(TEvent),
            (sp, e, ct) => sp.GetRequiredService<THandler>().HandleAsync((TEvent)e, ct));
        return this;
    }

    internal bool TryGet(string eventType, out Entry entry) => _entries.TryGetValue(eventType, out entry!);
}
