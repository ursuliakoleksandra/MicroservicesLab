using System.Text.Json.Serialization;
using Catalog.Contracts;
using Orders.Contracts;
using Platform.Aggregator.Caching;
using Grpc.Health.V1;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Platform.Aggregator.Clients;
using Platform.Aggregator.HealthChecks;
using Platform.Aggregator.Metrics;
using Platform.Aggregator.Resilience;
using Platform.Aggregator.Services;
using Platform.Caching;
using Platform.Security.Authentication;
using Platform.Security.Authorization;
using Platform.Security.ClientCredentials;
using Platform.ServiceDefaults;
using Reviews.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();      // Serilog, OpenTelemetry, health checks, service discovery, CorrelationIdHandler

// Лаб. 09: вхідні токени (Local / IdentityServer / Keycloak) та вихідний токен Client Credentials до downstream-сервісів
builder.AddPlatformAuthentication();
builder.Services.AddPlatformAuthorization();
builder.Services.AddClientCredentials(builder.Configuration);

// ---------- транспорт до мікросервісів: Grpc (за замовчуванням) або Http (Лаб. 05) — для порівняння затримок ----------
var transport = builder.Configuration["Aggregator:Transport"] ?? "Grpc";

// Лаб. 10: gRPC-адреса. Локально (Aspire) — HTTPS + ALPN: "https://{0}". У Kubernetes TLS немає, тому gRPC іде на окремий
// HTTP/2-порт 8081 (h2c): ConfigMap задає Aggregator__GrpcAddressFormat="http://_grpc.{0}" (endpoint з назвою "grpc",
// ключ services__<name>__grpc__0), а звичайний HTTP-трафік лишається на порту 8080.
var grpcAddressFormat = builder.Configuration["Aggregator:GrpcAddressFormat"] ?? "https://{0}";
Uri GrpcAddress(string service) => new(string.Format(grpcAddressFormat, service));

if (string.Equals(transport, "Http", StringComparison.OrdinalIgnoreCase))
{
    // Typed HttpClients: адреси за логічними іменами ресурсів AppHost ("https+http" = HTTPS, якщо є, інакше HTTP)
    // Resilience: стандартний обробник з ServiceDefaults замінюється профілем сервісу (RemoveAllResilienceHandlers усередині) —
    // критичні Замовлення й Каталог мають більше повторів і чутливіший breaker, некритичні Відгуки швидко «здаються».
    builder.Services.AddHttpClient<IOrdersClient, OrdersClient>(c => c.BaseAddress = new("https+http://orders-api"))
        .AddServiceResilience(ServiceProfiles.Orders)
        .AddHttpMessageHandler<ClientCredentialsTokenHandler>();   // Bearer-токен Client Credentials
    builder.Services.AddHttpClient<ICatalogClient, CatalogClient>(c => c.BaseAddress = new("https+http://catalog-api"))
        .AddServiceResilience(ServiceProfiles.Catalog)
        .AddHttpMessageHandler<ClientCredentialsTokenHandler>();
    builder.Services.AddHttpClient<IReviewsClient, ReviewsClient>(c => c.BaseAddress = new("https+http://reviews-api"))
        .AddServiceResilience(ServiceProfiles.Reviews)
        .AddHttpMessageHandler<ClientCredentialsTokenHandler>();
}
else
{
    // Типізовані gRPC-клієнти, згенеровані з .proto. gRPC потребує HTTP/2 -> HTTPS-endpoint сервісу (ALPN).
    // Клієнти створює та сама фабрика HttpClient, тож діють service discovery, resilience і CorrelationIdHandler
    // (X-Correlation-Id іде в метадані виклику без жодного коду в Aggregator). Канал мультиплексує паралельні виклики.
    //
    // Лаб. 07: для кожного клієнта (1) retry за gRPC-СТАТУСАМИ (ServiceConfig каналу: HTTP-обробник їх не бачить, бо статус іде
    // в трейлерах відповіді з HTTP 200) і (2) HTTP-рівень (мережеві збої, таймаути, circuit breaker) за профілем сервісу.
    builder.Services.AddGrpcClient<OrdersService.OrdersServiceClient>(o => o.Address = GrpcAddress("orders-api"))
        .AddGrpcRetryPolicy(ServiceProfiles.Orders)
        .AddServiceResilience(ServiceProfiles.Orders, forGrpc: true)
        .AddHttpMessageHandler<ClientCredentialsTokenHandler>();   // Bearer-токен Client Credentials
    builder.Services.AddGrpcClient<CatalogService.CatalogServiceClient>(o => o.Address = GrpcAddress("catalog-api"))
        .AddGrpcRetryPolicy(ServiceProfiles.Catalog)
        .AddServiceResilience(ServiceProfiles.Catalog, forGrpc: true)
        .AddHttpMessageHandler<ClientCredentialsTokenHandler>();
    builder.Services.AddGrpcClient<ReviewsService.ReviewsServiceClient>(o => o.Address = GrpcAddress("reviews-api"))
        .AddGrpcRetryPolicy(ServiceProfiles.Reviews)
        .AddServiceResilience(ServiceProfiles.Reviews, forGrpc: true)
        .AddHttpMessageHandler<ClientCredentialsTokenHandler>();

    builder.Services.AddTransient<IOrdersClient, GrpcOrdersClient>();
    builder.Services.AddTransient<ICatalogClient, GrpcCatalogClient>();
    builder.Services.AddTransient<IReviewsClient, GrpcReviewsClient>();
}

// ---------- кеш агрегованих відповідей: лише Redis (L1 вимкнено політикою), TTL 30 с ----------
builder.Services.AddMemoryCache(o => o.SizeLimit = 1_000);        // потрібен ITwoLevelCache, хоча L1 тут не використовується
if (!string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("redis")))
    builder.AddRedisDistributedCache("redis",                     // ConnectionStrings:redis передає AppHost
        configureSettings: settings => settings.DisableHealthChecks = true);   // власна перевірка "redis-cache" нижче (Degraded, а не Unhealthy)
else
    builder.Services.AddDistributedMemoryCache();                 // запуск без Aspire
builder.Services.AddTwoLevelCache("Aggregator.Cache");

builder.Services.AddSingleton<AggregatorMetrics>();               // Лаб. 07: бізнес-метрики (IMeterFactory, Meter "Platform.Aggregator")

// ---------- Лаб. 07: health checks залежностей (readiness) ----------
// gRPC Health Checking Protocol: клієнти Health.HealthClient за іменами; критичні сервіси -> Unhealthy, некритичний (Відгуки) -> Degraded
builder.Services.AddGrpcClient<Health.HealthClient>("orders-health", o => o.Address = GrpcAddress("orders-api"));
builder.Services.AddGrpcClient<Health.HealthClient>("catalog-health", o => o.Address = GrpcAddress("catalog-api"));
builder.Services.AddGrpcClient<Health.HealthClient>("reviews-health", o => o.Address = GrpcAddress("reviews-api"));

var healthChecks = builder.Services.AddHealthChecks()
    .AddTypeActivatedCheck<GrpcDependencyHealthCheck>("orders-api", failureStatus: HealthStatus.Unhealthy,
        tags: ["grpc", "downstream", "ready"], args: ["orders-health", "orders-api", HealthStatus.Unhealthy])
    .AddTypeActivatedCheck<GrpcDependencyHealthCheck>("catalog-api", failureStatus: HealthStatus.Unhealthy,
        tags: ["grpc", "downstream", "ready"], args: ["catalog-health", "catalog-api", HealthStatus.Unhealthy])
    .AddTypeActivatedCheck<GrpcDependencyHealthCheck>("reviews-api", failureStatus: HealthStatus.Degraded,
        tags: ["grpc", "downstream", "ready"], args: ["reviews-health", "reviews-api", HealthStatus.Degraded]);

var redisConnection = builder.Configuration.GetConnectionString("redis");
if (!string.IsNullOrWhiteSpace(redisConnection))
    healthChecks.AddRedis(redisConnection, name: "redis-cache", failureStatus: HealthStatus.Degraded,   // кеш необов'язковий: без нього сервіс працює
        tags: ["cache", "redis", "ready"], timeout: TimeSpan.FromSeconds(3));

builder.Services.AddScoped<OrderAggregationService>();
builder.Services.AddScoped<CachedOrderAggregationService>();
builder.Services.AddScoped<IOrderAggregationService>(sp => sp.GetRequiredService<CachedOrderAggregationService>());

// camelCase — типово для MVC; null-значення не серіалізуються.
// ConfigureHttpJsonOptions діє лише на minimal API, тому для контролерів — AddJsonOptions.
builder.Services.AddControllers().AddJsonOptions(o =>
    o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);

builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
    ctx.ProblemDetails.Extensions["traceId"] = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? ctx.HttpContext.TraceIdentifier);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

ResilienceLog.Factory = app.Services.GetRequiredService<ILoggerFactory>();   // логування переходів circuit breaker
app.Logger.LogInformation("Aggregator transport to microservices: {Transport}", transport);

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();         // непередбачені помилки -> ProblemDetails 500

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapDefaultEndpoints();         // /health і /alive
app.Run();

public partial class Program;
