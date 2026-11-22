using Orders.Api.ErrorHandling;
using Orders.Api.GrpcServices;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Platform.ServiceDefaults;
using Orders.Bll.Mapping;
using Orders.Bll.Services;
using Orders.Dal;
using Orders.Dal.Interfaces;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();      // Лаб. 05: Serilog, OpenTelemetry, health checks, service discovery

// Рядок підключення береться з appsettings.json і перевизначається змінною оточення
// ConnectionStrings__OrdersDb (саме так його передає .NET Aspire через WithReference).
var connectionString = builder.Configuration.GetConnectionString("OrdersDb")
    ?? throw new InvalidOperationException("Connection string 'OrdersDb' is missing");

// ---------- DI: Unit of Work (Scoped = один екземпляр і одне з'єднання на HTTP-запит) і сервіси ----------
builder.Services.AddScoped<IUnitOfWork>(_ => new UnitOfWork(connectionString));
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IProductService, ProductService>();

// ---------- AutoMapper: усі профілі зібрано централізовано в Orders.Bll ----------
builder.Services.AddAutoMapper(cfg =>
{
    cfg.LicenseKey = builder.Configuration["AutoMapper:LicenseKey"];   // Community-ключ з automapper.io (необов'язково)
    cfg.AddMaps(typeof(OrderProfile).Assembly);
});

// ---------- Web/API ----------
builder.Services.AddControllers();

builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
    ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

// ---------- Лаб. 06: gRPC (HTTP/2; ALPN на HTTPS-endpoint) + interceptor логування викликів ----------
builder.Services.AddGrpc(o => o.Interceptors.Add<GrpcLoggingInterceptor>());

// gRPC працює поверх HTTP/2. Один endpoint обслуговує і REST (HTTP/1.1 через Gateway), і gRPC: на HTTPS протокол
// узгоджується автоматично (ALPN). Без TLS для gRPC потрібен окремий endpoint лише з HTTP/2.
builder.WebHost.ConfigureKestrel(k => k.ConfigureEndpointDefaults(l => l.Protocols = HttpProtocols.Http1AndHttp2));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();            // глобальний обробник: винятки -> ProblemDetails

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
}

app.MapControllers();
app.MapGrpcService<OrdersGrpcService>();
app.MapDefaultEndpoints();         // /health і /alive
app.Run();

public partial class Program;        // для WebApplicationFactory (API-тести)
