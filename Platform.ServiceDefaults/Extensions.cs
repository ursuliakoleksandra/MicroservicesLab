using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
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

    /// <summary>Імена ActivitySource, які реєструються в трасуванні (власні spans бізнес-операцій).</summary>
    /// <summary>Meter-и кешу: Catalog (L1 + L2) і Aggregator (агрегована відповідь в Redis).</summary>
    private static readonly string[] CacheMeters = ["Catalog.Cache", "Aggregator.Cache"];

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

            http.AddStandardResilienceHandler(o =>
            {
                o.Retry.MaxRetryAttempts = 2;                      // менше повторів — часткова відповідь Aggregator приходить швидше
                o.Retry.Delay = TimeSpan.FromMilliseconds(300);
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
                .AddMeter("Grpc.AspNetCore.Server", "Grpc.Net.Client"))   // метрики gRPC-викликів
            .WithTracing(tracing =>
            {
                tracing
                    .SetSampler(isDevelopment
                        ? new AlwaysOnSampler()
                        : new ParentBasedSampler(new TraceIdRatioBasedSampler(samplingRatio)))
                    .AddSource(builder.Environment.ApplicationName)
                    .AddSource(CustomActivitySources)                                         // власні spans (ч. 3.3)
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
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);   // перевірка «процес живий»

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // як у шаблоні Aspire: лише в Development (у Production відкриття health checks — окреме рішення, Лаб. 07/10)
        if (app.Environment.IsDevelopment())
        {
            app.MapHealthChecks(HealthEndpointPath);                              // усі перевірки (у т.ч. MongoDB у Reviews)
            app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains("live")
            });
        }

        return app;
    }
}
