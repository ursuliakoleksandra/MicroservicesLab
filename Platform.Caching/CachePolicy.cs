namespace Platform.Caching;

/// <summary>
/// Політика запису в кеш. L1 (пам'ять) завжди живе коротше за L2 (Redis): інакше застарілі дані в пам'яті
/// переживуть оновлення в Redis. Якщо задано більший L1, він обмежується значенням L2.
/// </summary>
public sealed record CachePolicy
{
    /// <summary>Абсолютний TTL у пам'яті; <c>null</c> — L1 не використовується (лише Redis).</summary>
    public TimeSpan? L1Absolute { get; init; }

    /// <summary>Ковзний TTL у пам'яті (для часто запитуваних записів); не довший за абсолютний.</summary>
    public TimeSpan? L1Sliding { get; init; }

    /// <summary>Абсолютний TTL у Redis.</summary>
    public TimeSpan L2Absolute { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>«Вага» запису для SizeLimit кешу в пам'яті (в одиницях, а не в байтах).</summary>
    public int Size { get; init; } = 1;

    /// <summary>Ефективний абсолютний TTL L1 (не більший за L2) або <c>null</c>.</summary>
    public TimeSpan? EffectiveL1Absolute => L1Absolute is { } l1 ? (l1 < L2Absolute ? l1 : L2Absolute) : null;
}
