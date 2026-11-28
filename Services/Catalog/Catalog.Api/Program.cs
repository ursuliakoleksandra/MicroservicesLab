using Platform.Security;
using Platform.Security.Authentication;
using Platform.Security.Authorization;
using Catalog.Api.ErrorHandling;
using Catalog.Bll.Mapping;
using Catalog.Bll.Services;
using Catalog.Bll.Validators;
using Catalog.Dal;
using Catalog.Dal.Interfaces;
using Catalog.Dal.Seed;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Catalog.Api.Caching;
using Catalog.Api.GrpcServices;
using Catalog.Api.HealthChecks;
using Catalog.Api.Metrics;
using Platform.Caching;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Platform.Messaging;
using Platform.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();      // Лаб. 05: Serilog, OpenTelemetry, health checks, service discovery
builder.AddMessaging();            // Лаб. 08: RabbitMQ (Aspire.RabbitMQ.Client), publisher, топологія, health check

var connectionString = builder.Configuration.GetConnectionString("CatalogDb")
    ?? throw new InvalidOperationException("Connection string 'CatalogDb' is missing");

// ---------- EF Core ----------
builder.Services.AddDbContext<CatalogDbContext>(options =>
{
    options.UseSqlServer(connectionString, sql =>
    {
        sql.MigrationsAssembly(typeof(CatalogDbContext).Assembly.GetName().Name);
        sql.EnableRetryOnFailure();                   // БД може ще стартувати (Aspire/Docker)
    });
    if (builder.Environment.IsDevelopment()) options.EnableSensitiveDataLogging();

    // Початкову міграцію написано вручну (див. README): після перегенерації через dotnet ef вимкніть цей прапорець
    if (builder.Configuration.GetValue<bool>("Database:IgnorePendingModelChangesWarning"))
        options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
});

// ---------- Лаб. 06: кеш. L1 = IMemoryCache (SizeLimit), L2 = Redis (IDistributedCache) через Aspire ----------
builder.Services.AddMemoryCache(o =>
{
    o.SizeLimit = 10_000;              // у «одиницях» Size записів (CachePolicy.Size), а не в байтах
    o.TrackStatistics = true;          // розмір і заповненість L1 -> метрики та health check
});

if (!string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("redis")))
    builder.AddRedisDistributedCache("redis",                 // ConnectionStrings:redis передає AppHost (WithReference)
        configureSettings: settings => settings.DisableHealthChecks = true);   // власна перевірка "redis-cache" (Degraded, а не Unhealthy)
else
    builder.Services.AddDistributedMemoryCache();             // запуск без Aspire: L2 — у пам'яті процесу

builder.Services.AddTwoLevelCache("Catalog.Cache");           // ITwoLevelCache + метрики cache.hits / cache.misses
builder.Services.AddHostedService<CacheWarmupService>();      // прогрівання довідників і перших товарів

// ---------- DI: Unit of Work (Scoped = один DbContext на запит) і сервіси ----------
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IBrandService, BrandService>();

// ---------- AutoMapper + FluentValidation (валідатори в окремих класах, виклик явний у контролерах) ----------
builder.Services.AddAutoMapper(cfg =>
{
    cfg.LicenseKey = builder.Configuration["AutoMapper:LicenseKey"];
    cfg.AddMaps(typeof(ProductProfile).Assembly);
});
builder.Services.AddValidatorsFromAssemblyContaining<CreateProductDtoValidator>();

// ---------- Web/API ----------
// Лаб. 07: бізнес-метрики (Singleton, Meter "Catalog.Api") і action filter, що їх збирає
builder.Services.AddSingleton<CatalogMetrics>();
builder.Services.AddControllers(o => o.Filters.Add<CatalogMetricsFilter>());                    // DataAnnotations -> автоматичний 400 ValidationProblemDetails

builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
    ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

// ---------- Лаб. 06: gRPC (HTTP/2; ALPN на HTTPS-endpoint) + interceptor логування викликів ----------
builder.Services.AddGrpc(o => o.Interceptors.Add<GrpcLoggingInterceptor>());

// gRPC працює поверх HTTP/2. Один endpoint обслуговує і REST (HTTP/1.1 через Gateway), і gRPC: на HTTPS протокол
// узгоджується автоматично (ALPN). Без TLS для gRPC потрібен окремий endpoint лише з HTTP/2.
builder.WebHost.ConfigureKestrel(k => k.ConfigureEndpointDefaults(l => l.Protocols = HttpProtocols.Http1AndHttp2));
// Лаб. 07: SQL Server, Redis (Degraded), статистика кешу та gRPC Health Checking Protocol
builder.Services.AddCatalogHealthChecks(connectionString, builder.Configuration.GetConnectionString("redis"));

// ---------- Лаб. 09: автентифікація (Local / IdentityServer / Keycloak) і авторизація за дозволами ----------
builder.AddPlatformAuthentication(ApiResourceNames.Products);
builder.Services.AddPlatformAuthorization();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ---------- міграції + ідемпотентний програмний сідінг (довідники — HasData у конфігураціях) ----------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
        await db.Database.MigrateAsync();
    if (app.Configuration.GetValue("Database:Seed", !app.Environment.IsProduction()))
        await DbInitializer.SeedAsync(db);
}

app.UseMiddleware<CorrelationIdMiddleware>();   // спільний, з ServiceDefaults
app.UseExceptionHandler();                            // глобальна обробка: виняток -> ProblemDetails (логується один раз)

app.UseAuthentication();            // Лаб. 09: JWT (Local / IdentityServer / Keycloak)
app.UseAuthorization();             // [RequirePermission] і scope-політики

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
}

app.MapControllers();
app.MapGrpcService<CatalogGrpcService>();
app.MapGrpcHealthChecksService();     // grpc.health.v1.Health: використовує Aggregator
app.MapDefaultEndpoints();         // /health і /alive
app.Run();

public partial class Program;                         // для WebApplicationFactory (API-тести)
