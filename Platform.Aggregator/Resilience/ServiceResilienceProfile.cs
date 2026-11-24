namespace Platform.Aggregator.Resilience;

/// <summary>
/// Політика стійкості виклику одного мікросервісу (Лаб. 07, ч. 1.6). Значення підібрано за КРИТИЧНІСТЮ сервісу для сценарію
/// «повна картка замовлення»; обґрунтування — у README (розділ «Політики resilience»).
/// </summary>
public sealed record ServiceResilienceProfile(
    string Name,
    bool Critical,
    int HttpRetries,
    TimeSpan HttpRetryDelay,
    TimeSpan AttemptTimeout,
    TimeSpan TotalTimeout,
    double BreakerFailureRatio,
    int BreakerMinimumThroughput,
    TimeSpan BreakerDuration,
    int GrpcMaxAttempts,
    TimeSpan GrpcInitialBackoff,
    TimeSpan GrpcMaxBackoff,
    TimeSpan GrpcDeadline)
{
    /// <summary>Вікно, у якому circuit breaker рахує частку збоїв. Має бути ≥ 2 × AttemptTimeout, інакше валідація опцій не пройде.</summary>
    public static readonly TimeSpan SamplingDuration = TimeSpan.FromSeconds(30);
}

public static class ServiceProfiles
{
    /// <summary>
    /// Замовлення — КРИТИЧНИЙ: без замовлення агрегувати нічого. Багато повторів (read-ідемпотентні GET/RPC),
    /// чутливіший breaker (0.4), щоб швидко припинити марні виклики, помірні таймаути.
    /// </summary>
    public static readonly ServiceResilienceProfile Orders = new(
        Name: "orders-api", Critical: true,
        HttpRetries: 3, HttpRetryDelay: TimeSpan.FromMilliseconds(500),
        AttemptTimeout: TimeSpan.FromSeconds(3), TotalTimeout: TimeSpan.FromSeconds(20),
        BreakerFailureRatio: 0.4, BreakerMinimumThroughput: 10, BreakerDuration: TimeSpan.FromSeconds(15),
        GrpcMaxAttempts: 3, GrpcInitialBackoff: TimeSpan.FromMilliseconds(200), GrpcMaxBackoff: TimeSpan.FromSeconds(1),
        GrpcDeadline: TimeSpan.FromSeconds(5));

    /// <summary>
    /// Каталог — КРИТИЧНИЙ: без товарів картку замовлення не зібрати змістовно. Найбільше повторів (4), найчутливіший breaker (0.3),
    /// довший загальний таймаут, щоб повтори встигли виконатися (5 спроб × 3 с + затримки ≈ 22 с).
    /// </summary>
    public static readonly ServiceResilienceProfile Catalog = new(
        Name: "catalog-api", Critical: true,
        HttpRetries: 4, HttpRetryDelay: TimeSpan.FromMilliseconds(500),
        AttemptTimeout: TimeSpan.FromSeconds(3), TotalTimeout: TimeSpan.FromSeconds(25),
        BreakerFailureRatio: 0.3, BreakerMinimumThroughput: 10, BreakerDuration: TimeSpan.FromSeconds(15),
        GrpcMaxAttempts: 4, GrpcInitialBackoff: TimeSpan.FromMilliseconds(200), GrpcMaxBackoff: TimeSpan.FromSeconds(2),
        GrpcDeadline: TimeSpan.FromSeconds(6));

    /// <summary>
    /// Відгуки — НЕКРИТИЧНИЙ: без них Aggregator повертає часткову відповідь (Лаб. 05). Швидко здатися: 1 повтор,
    /// короткі таймаути (загалом ≤ 4 с), нечутливий breaker (0.7) — поодинокі збої не розмикають ланцюг, а постійні
    /// відсікаються на довше (30 с не потрібні — 10 с достатньо для відновлення).
    /// </summary>
    public static readonly ServiceResilienceProfile Reviews = new(
        Name: "reviews-api", Critical: false,
        HttpRetries: 1, HttpRetryDelay: TimeSpan.FromMilliseconds(200),
        AttemptTimeout: TimeSpan.FromMilliseconds(1500), TotalTimeout: TimeSpan.FromSeconds(4),
        BreakerFailureRatio: 0.7, BreakerMinimumThroughput: 10, BreakerDuration: TimeSpan.FromSeconds(10),
        GrpcMaxAttempts: 2, GrpcInitialBackoff: TimeSpan.FromMilliseconds(100), GrpcMaxBackoff: TimeSpan.FromMilliseconds(500),
        GrpcDeadline: TimeSpan.FromSeconds(2));

    public static readonly IReadOnlyList<ServiceResilienceProfile> All = [Orders, Catalog, Reviews];
}
