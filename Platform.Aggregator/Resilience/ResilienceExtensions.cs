using Grpc.Core;
using Grpc.Net.Client.Configuration;
using Polly;

namespace Platform.Aggregator.Resilience;

/// <summary>Логування переходів circuit breaker. Polly додатково пише власні події (категорія "Polly") і метрики (Meter "Polly").</summary>
public static class ResilienceLog
{
    /// <summary>Встановлюється в Program.cs після побудови застосунку; до того події просто не логуються.</summary>
    public static ILoggerFactory? Factory { get; set; }

    private static ILogger? Logger => Factory?.CreateLogger("Platform.Aggregator.Resilience");

    public static void Opened(string service, TimeSpan breakDuration) =>
        Logger?.LogError("Circuit breaker for {Service} OPENED: calls are rejected for {BreakDuration} s (fail-fast)", service, breakDuration.TotalSeconds);

    public static void HalfOpened(string service) =>
        Logger?.LogWarning("Circuit breaker for {Service} HALF-OPEN: a trial call is allowed", service);

    public static void Closed(string service) =>
        Logger?.LogInformation("Circuit breaker for {Service} CLOSED: the service has recovered", service);
}

public static class ResilienceExtensions
{
    /// <summary>
    /// Замінює стандартну політику з ServiceDefaults на власну для конкретного клієнта. RemoveAllResilienceHandlers() ОБОВ'ЯЗКОВИЙ:
    /// без нього AddStandardResilienceHandler() додався б другим обробником поверх першого (нашарування повторів = лавина запитів).
    /// </summary>
    public static IHttpClientBuilder AddServiceResilience(this IHttpClientBuilder builder, ServiceResilienceProfile p,
        bool forGrpc = false)
    {
        // для gRPC дедлайн виклику діє на весь виклик із повторами, тому HTTP-таймаути мають бути трохи більшими за нього
        var attemptTimeout = forGrpc ? p.GrpcDeadline + TimeSpan.FromSeconds(1) : p.AttemptTimeout;
        var totalTimeout = forGrpc ? p.GrpcDeadline + TimeSpan.FromSeconds(2) : p.TotalTimeout;

        builder.RemoveAllResilienceHandlers();
        builder.AddStandardResilienceHandler(o =>
        {
            // --- retry: експоненційна затримка + jitter; небезпечні методи не повторюємо ---
            o.Retry.MaxRetryAttempts = forGrpc ? 1 : p.HttpRetries;     // gRPC-статуси повторює ServiceConfig, а не цей обробник
            o.Retry.Delay = p.HttpRetryDelay;
            o.Retry.BackoffType = DelayBackoffType.Exponential;
            o.Retry.UseJitter = true;

            // HTTP-клієнти (GET) — небезпечні методи не повторюємо. gRPC-виклики на HTTP-рівні — це POST, але всі методи Aggregator
            // лише читають (ідемпотентні), тому для gRPC-клієнтів HTTP-повтор МЕРЕЖЕВИХ збоїв дозволено: так події OnRetry видно
            // в метриках Polly, а загальну кількість спроб обмежує дедлайн виклику.
            if (!forGrpc) o.Retry.DisableForUnsafeHttpMethods();

            // --- таймаути: загальний має вміщувати повтори, інакше повтори не встигнуть виконатися ---
            o.AttemptTimeout.Timeout = attemptTimeout;
            o.TotalRequestTimeout.Timeout = totalTimeout;

            // --- circuit breaker: частка збоїв у вікні; MinimumThroughput знижено для навчального стенду з малим трафіком ---
            o.CircuitBreaker.FailureRatio = p.BreakerFailureRatio;
            o.CircuitBreaker.MinimumThroughput = p.BreakerMinimumThroughput;
            o.CircuitBreaker.SamplingDuration = ServiceResilienceProfile.SamplingDuration;
            o.CircuitBreaker.BreakDuration = p.BreakerDuration;
            o.CircuitBreaker.OnOpened = args =>
            {
                ResilienceLog.Opened(p.Name, args.BreakDuration);
                return ValueTask.CompletedTask;
            };
            o.CircuitBreaker.OnHalfOpened = _ =>
            {
                ResilienceLog.HalfOpened(p.Name);
                return ValueTask.CompletedTask;
            };
            o.CircuitBreaker.OnClosed = _ =>
            {
                ResilienceLog.Closed(p.Name);
                return ValueTask.CompletedTask;
            };
        });

        return builder;
    }

    /// <summary>
    /// gRPC повертає помилки (Unavailable, DeadlineExceeded) статусом у трейлерах відповіді з HTTP 200, тому HTTP-обробник їх НЕ повторює.
    /// Повтори за gRPC-статусами задає ServiceConfig каналу: лише тимчасові статуси, експоненційний backoff.
    /// </summary>
    public static IHttpClientBuilder AddGrpcRetryPolicy(this IHttpClientBuilder builder, ServiceResilienceProfile p) =>
        builder.ConfigureChannel(channel =>
        {
            channel.ThrowOperationCanceledOnCancellation = true;
            channel.ServiceConfig = new ServiceConfig
            {
                MethodConfigs =
                {
                    new MethodConfig
                    {
                        Names = { MethodName.Default },                       // усі методи сервісу — це читання, тобто ідемпотентні
                        RetryPolicy = new RetryPolicy
                        {
                            MaxAttempts = p.GrpcMaxAttempts,                  // разом із першою спробою (максимум 5)
                            InitialBackoff = p.GrpcInitialBackoff,
                            MaxBackoff = p.GrpcMaxBackoff,
                            BackoffMultiplier = 2,
                            RetryableStatusCodes = { StatusCode.Unavailable, StatusCode.ResourceExhausted }
                        }
                    }
                }
            };
        });
}
