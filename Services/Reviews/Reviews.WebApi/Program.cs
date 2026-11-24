using AutoMapper;
using Reviews.Application;
using Reviews.Application.Common.Mappings;
using Reviews.Infrastructure;
using Reviews.WebApi.ErrorHandling;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Platform.ServiceDefaults;
using MediatR;
using Reviews.WebApi.GrpcServices;
using Reviews.WebApi.Metrics;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();      // Лаб. 05: Serilog, OpenTelemetry, health checks (у т.ч. MongoDB), service discovery

// ---------- кожен шар надає власний метод розширення ----------
builder.Services
    .AddApplication(builder.Configuration)          // MediatR, behaviors, валідатори FluentValidation
    .AddInfrastructure(builder.Configuration);      // MongoDB: контекст, BSON-мапінг, репозиторії, індекси, сідінг, health check

builder.Services.AddAutoMapper(cfg =>
{
    cfg.LicenseKey = builder.Configuration["LuckyPenny:LicenseKey"];
    cfg.AddMaps(typeof(ReviewProfile).Assembly);
});

// ---------- Web API ----------
builder.Services.AddControllers();

// Лаб. 07: бізнес-метрики (Singleton, Meter "Reviews.Api"); MetricsBehavior — найближчий до обробника behavior MediatR
builder.Services.AddSingleton<ReviewMetrics>();
builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(MetricsBehavior<,>));
builder.Services.AddGrpcHealthChecks(o => o.Services.MapService("", r => r.Tags.Contains("ready")));

builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
    ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

// ---------- Лаб. 06: gRPC (HTTP/2; ALPN на HTTPS-endpoint) + interceptor логування викликів ----------
builder.Services.AddGrpc(o => o.Interceptors.Add<GrpcLoggingInterceptor>());

// gRPC працює поверх HTTP/2. Один endpoint обслуговує і REST (HTTP/1.1 через Gateway), і gRPC: на HTTPS протокол
// узгоджується автоматично (ALPN). Без TLS для gRPC потрібен окремий endpoint лише з HTTP/2.
builder.WebHost.ConfigureKestrel(k => k.ConfigureEndpointDefaults(l => l.Protocols = HttpProtocols.Http1AndHttp2));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
}

app.MapControllers();
app.MapGrpcService<ReviewsGrpcService>();
app.MapGrpcHealthChecksService();     // grpc.health.v1.Health: використовує Aggregator
app.MapDefaultEndpoints();         // /health (у т.ч. перевірка MongoDB) і /alive
app.Run();

public partial class Program;                       // для WebApplicationFactory (API-тести)
