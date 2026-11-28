using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Polly;
using Platform.ServiceDefaults;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

// Простір імен збігається з шаблоном Aspire: у Program.cs сервісів достатньо викликати builder.AddServiceDefaults().
namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Спільні налаштування всіх сервісів платформи: Serilog (структуровані логи), OpenTelemetry (трасування, метрики, OTLP),
/// health checks, service discovery, resilience та CorrelationId для вихідних HTTP-викликів.
/// </summary>
public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";
    private const string LivenessPath = "/health/live";
    private const string ReadinessPath = "/health/ready";
    private const string StartupPath = "/health/startup";

    /// <summary>Імена ActivitySource, які реєструються в трасуванні (власні spans бізнес-операцій).</summary>
    /// <summary>Meter-и кешу: Catalog (L1 + L2) і Aggregator (агрегована відповідь в Redis).</summary>
    private static readonly string[] CacheMeters = ["Catalog.Cache", "Aggregator.Cache"];

    /// <summary>Бізнес-метрики сервісів (Лаб. 07, ч. 2): один Meter на сервіс, ім'я = назва сервісу.</summary>
    private static readonly string[] BusinessMeters = ["Orders.Api", "Catalog.Api", "Reviews.Api", "Platform.Aggregator", "Platform.Messaging"];

    private static readonly string[] CustomActivitySources = ["Orders.Api", "Catalog.Api", "Reviews.WebApi", "Platform.Aggregator"];

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        // Serilog стає основним провайдером; стандартний Console-провайдер прибираємо, щоб не дублювати рядки в консолі.
        builder.Logging.ClearProviders();

        builder.ConfigureOpenTelemetry();   // додає OpenTelemetry-провайдер логів, трасування, метрики, OTLP
        builder.ConfigureSerilog();         // Serilog + writeToProviders -> OpenTelemetry -> Structured logs у Dashboard
        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddTransient<CorrelationIdHandler>();

        var isDevelopment = builder.Environment.IsDevelopment();
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Лаб. 06: gRPC-клієнти створюються тією ж фабрикою HttpClient, тому отримують і service discovery,
            // і resilience, і CorrelationId (він іде в HTTP/2-заголовки = метадані gRPC-виклику).
            http.ConfigurePrimaryHttpMessageHandler(() =>
            {
                var handler = new SocketsHttpHandler
                {
                    // рекомендації щодо продуктивності gRPC: тримати з'єднання й не втрачати його через простій
                    PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
                    KeepAlivePingDelay = TimeSpan.FromSeconds(60),
                    KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
                    EnableMultipleHttp2Connections = true
                };
                // лише Development: сервіси спілкуються по HTTPS із dev-сертифікатом, який може бути не довіреним
                if (isDevelopment) handler.SslOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true;
                return handler;
            });

            // Лаб. 07: Standard Resilience Handler підключається ЛИШЕ тут (для всіх клієнтів, включно з gRPC-клієнтами).
            // Він поєднує (зовні всередину): rate limiter -> загальний таймаут -> retry -> circuit breaker -> таймаут спроби.
            // Клієнт із власною політикою спершу викликає RemoveAllResilienceHandlers() — обробники не нашаровуються
            // (див. Platform.Aggregator/Resilience).
            http.AddStandardResilienceHandler(o =>
            {
                o.Retry.MaxRetryAttempts = 2;                          // типово 3; менше повторів — часткова відповідь приходить швидше
                o.Retry.Delay = TimeSpan.FromMilliseconds(300);        // типово 2 с
                o.Retry.BackoffType = DelayBackoffType.Exponential;    // 0.3 с, 0.6 с, ...
                o.Retry.UseJitter = true;                              // розкид затримок проти «thundering herd»
                o.Retry.DisableForUnsafeHttpMethods();                 // POST/PUT/DELETE не повторюються: інакше можна створити замовлення двічі
            });
            http.AddServiceDiscovery();                            // http://orders-api -> реальна адреса від Aspire
            http.AddHttpMessageHandler<CorrelationIdHandler>();    // X-Correlation-Id у вихідні запити
        });

        return builder;
    }

    public static TBuilder ConfigureSerilog<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var environment = builder.Environment;
        var serviceName = builder.Configuration["OTEL_SERVICE_NAME"] ?? environment.ApplicationName;   // Aspire: ім'я ресурсу

        builder.Services.AddSerilog((services, cfg) => cfg
                .MinimumLevel.Is(environment.IsDevelopment() ? LogEventLevel.Debug : LogEventLevel.Information)
                .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.Extensions.Http", LogEventLevel.Warning)
                .MinimumLevel.Override("MongoDB", LogEventLevel.Warning)
                // налаштування конкретного сервісу з appsettings.json (секція Serilog) мають пріоритет над типовими
                .ReadFrom.Configuration(builder.Configuration)
                .Enrich.FromLogContext()                                         // CorrelationId з CorrelationIdMiddleware
                .Enrich.WithProperty("ServiceName", serviceName)
                .Enrich.WithProperty("Environment", environment.EnvironmentName)
                .Enrich.WithMachineName()
                .Enrich.WithThreadId()
                .WriteTo.Console(new CompactJsonFormatter()),                    // JSON у вкладці Console logs
            writeToProviders: true);                                             // -> провайдер OpenTelemetry -> Structured logs

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(o =>
        {
            o.IncludeFormattedMessage = true;
            o.IncludeScopes = true;
        });

        var isDevelopment = builder.Environment.IsDevelopment();
        // sampling: 100 % у Development; у Production — частка з Telemetry:SamplingRatio (типово 10 %)
        var samplingRatio = builder.Configuration.GetValue("Telemetry:SamplingRatio", 0.1);

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddAttributes(new KeyValuePair<string, object>[]
            {
                new("deployment.environment", builder.Environment.EnvironmentName)
            }))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(CacheMeters)                       // cache.hits / cache.misses та ін. (Лаб. 06, ч. 6.3)
                .AddMeter(BusinessMeters)                    // orders.created, products.updated, reviews.created, aggregator.* (Лаб. 07)
                .AddMeter("Polly")                           // resilience.polly.strategy.events: OnRetry, OnCircuitOpened, OnTimeout ... (Лаб. 07, ч. 1.8)
                .AddMeter("Grpc.AspNetCore.Server", "Grpc.Net.Client")   // метрики gRPC-викликів
                // межі кошиків гістограм тривалості (мс): типові межі OpenTelemetry розраховані на секунди, а не на мілісекунди
                .AddView("*.operation.duration", new ExplicitBucketHistogramConfiguration
                {
                    Boundaries = [1, 5, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000]
                }))
            .WithTracing(tracing =>
            {
                tracing
                    .SetSampler(isDevelopment
                        ? new AlwaysOnSampler()
                        : new ParentBasedSampler(new TraceIdRatioBasedSampler(samplingRatio)))
                    .AddSource(builder.Environment.ApplicationName)
                    .AddSource(CustomActivitySources)                                         // власні spans (ч. 3.3)
                    .AddSource("Platform.Messaging")                                          // Лаб. 08: Publish / Consume spans RabbitMQ
                    .AddSource("MongoDB.Driver.Core.Extensions.DiagnosticSources")            // MongoDB (Лаб. 04)
                    .AddAspNetCoreInstrumentation(o => o.Filter = ctx =>
                        !ctx.Request.Path.StartsWithSegments(HealthEndpointPath)
                        && !ctx.Request.Path.StartsWithSegments(AlivenessEndpointPath))
                    .AddHttpClientInstrumentation()
                    .AddSqlClientInstrumentation(o => o.SetDbStatementForText = isDevelopment);   // Dapper та EF Core
            });

        // адресу колектора задає Aspire (OTEL_EXPORTER_OTLP_ENDPOINT); експорт батчевий, запити не блокує
        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
            builder.Services.AddOpenTelemetry().UseOtlpExporter();

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddSingleton<StartupHealthCheck>();

        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"])          // «процес живий» — liveness, без залежностей
            .AddCheck<StartupHealthCheck>("startup", tags: ["startup"]);             // «старт завершено» — startup

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Лаб. 07: liveness / readiness / startup окремо за ТЕГАМИ перевірок; доступні в усіх середовищах, бо їх використовують
        // оркестратори (Kubernetes, Лаб. 10). Деталі кожної перевірки (JSON) показуються лише в Development.
        var writer = HealthResponseWriter.Create(includeDetails: app.Environment.IsDevelopment());

        // liveness: чи живий процес. БЕЗ зовнішніх залежностей (лише перевірка "self"), відповідає за мілісекунди.
        // Збій -> оркестратор ПЕРЕЗАПУСКАЄ процес.
        app.MapHealthChecks(LivenessPath, new HealthCheckOptions { Predicate = r => r.Tags.Contains("live"), ResponseWriter = writer });

        // readiness: чи готовий приймати трафік (бази, Redis, downstream gRPC-сервіси). Degraded -> 200 (працює без кешу),
        // Unhealthy -> 503. Збій -> оркестратор тимчасово ПРИБИРАЄ екземпляр з балансування, не перезапускаючи його.
        app.MapHealthChecks(ReadinessPath, new HealthCheckOptions { Predicate = r => r.Tags.Contains("ready"), ResponseWriter = writer });

        // startup: чи завершився старт (міграції, сідінг, прогрівання) — для сервісів із повільним стартом
        app.MapHealthChecks(StartupPath, new HealthCheckOptions { Predicate = r => r.Tags.Contains("startup"), ResponseWriter = writer });

        // як у шаблоні Aspire: /health (усі перевірки) та /alive — лише в Development
        if (app.Environment.IsDevelopment())
        {
            app.MapHealthChecks(HealthEndpointPath, new HealthCheckOptions { ResponseWriter = writer });
            app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions { Predicate = r => r.Tags.Contains("live") });
        }

        return app;
    }
}
