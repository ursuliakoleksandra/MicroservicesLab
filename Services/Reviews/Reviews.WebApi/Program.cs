using AutoMapper;
using Reviews.Application;
using Reviews.Application.Common.Mappings;
using Reviews.Infrastructure;
using Reviews.WebApi.ErrorHandling;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ---------- Serilog: структуровані логи (CorrelationId, RequestPath) ----------
builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] ({CorrelationId}) {SourceContext} {Message:lj}{NewLine}{Exception}"));

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

builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
    ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
}

app.MapControllers();
app.MapHealthChecks("/health");
app.Run();

public partial class Program;                       // для WebApplicationFactory (API-тести)
