namespace Platform.ServiceDefaults.Correlation;

/// <summary>
/// Поточний CorrelationId у межах асинхронного потоку виконання (AsyncLocal). Встановлюється middleware для HTTP-запитів
/// і споживачем повідомлень (Лаб. 08); з нього CorrelationId потрапляє в події, що публікуються під час обробки.
/// </summary>
public static class CorrelationContext
{
    private static readonly AsyncLocal<string?> Holder = new();

    public static string? Current
    {
        get => Holder.Value;
        set => Holder.Value = value;
    }
}
