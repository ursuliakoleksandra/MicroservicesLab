using System.Text.Json.Serialization;
using Catalog.Contracts;
using Orders.Contracts;
using Platform.Aggregator.Caching;
using Platform.Aggregator.Clients;
using Platform.Aggregator.Services;
using Platform.Caching;
using Platform.ServiceDefaults;
using Reviews.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();      // Serilog, OpenTelemetry, health checks, service discovery, CorrelationIdHandler

// ---------- транспорт до мікросервісів: Grpc (за замовчуванням) або Http (Лаб. 05) — для порівняння затримок ----------
var transport = builder.Configuration["Aggregator:Transport"] ?? "Grpc";

if (string.Equals(transport, "Http", StringComparison.OrdinalIgnoreCase))
{
    // Typed HttpClients: адреси за логічними іменами ресурсів AppHost ("https+http" = HTTPS, якщо є, інакше HTTP)
    builder.Services.AddHttpClient<IOrdersClient, OrdersClient>(c => c.BaseAddress = new("https+http://orders-api"));
    builder.Services.AddHttpClient<ICatalogClient, CatalogClient>(c => c.BaseAddress = new("https+http://catalog-api"));
    builder.Services.AddHttpClient<IReviewsClient, ReviewsClient>(c => c.BaseAddress = new("https+http://reviews-api"));
}
else
{
    // Типізовані gRPC-клієнти, згенеровані з .proto. gRPC потребує HTTP/2 -> HTTPS-endpoint сервісу (ALPN).
    // Клієнти створює та сама фабрика HttpClient, тож діють service discovery, resilience і CorrelationIdHandler
    // (X-Correlation-Id іде в метадані виклику без жодного коду в Aggregator). Канал мультиплексує паралельні виклики.
    builder.Services.AddGrpcClient<OrdersService.OrdersServiceClient>(o => o.Address = new Uri("https://orders-api"))
        .ConfigureChannel(o => o.ThrowOperationCanceledOnCancellation = true);
    builder.Services.AddGrpcClient<CatalogService.CatalogServiceClient>(o => o.Address = new Uri("https://catalog-api"))
        .ConfigureChannel(o => o.ThrowOperationCanceledOnCancellation = true);
    builder.Services.AddGrpcClient<ReviewsService.ReviewsServiceClient>(o => o.Address = new Uri("https://reviews-api"))
        .ConfigureChannel(o => o.ThrowOperationCanceledOnCancellation = true);

    builder.Services.AddTransient<IOrdersClient, GrpcOrdersClient>();
    builder.Services.AddTransient<ICatalogClient, GrpcCatalogClient>();
    builder.Services.AddTransient<IReviewsClient, GrpcReviewsClient>();
}

// ---------- кеш агрегованих відповідей: лише Redis (L1 вимкнено політикою), TTL 30 с ----------
builder.Services.AddMemoryCache(o => o.SizeLimit = 1_000);        // потрібен ITwoLevelCache, хоча L1 тут не використовується
if (!string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("redis")))
    builder.AddRedisDistributedCache("redis");                    // ConnectionStrings:redis передає AppHost
else
    builder.Services.AddDistributedMemoryCache();                 // запуск без Aspire
builder.Services.AddTwoLevelCache("Aggregator.Cache");

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

app.Logger.LogInformation("Aggregator transport to microservices: {Transport}", transport);

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();         // непередбачені помилки -> ProblemDetails 500

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
}

app.MapControllers();
app.MapDefaultEndpoints();         // /health і /alive
app.Run();

public partial class Program;
