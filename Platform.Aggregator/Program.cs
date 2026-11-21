using System.Text.Json.Serialization;
using Platform.Aggregator.Clients;
using Platform.Aggregator.Services;
using Platform.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();      // Serilog, OpenTelemetry, health checks, service discovery, CorrelationIdHandler

// ---------- Typed HttpClients: адреса за логічними іменами ресурсів AppHost ("https+http" = HTTPS, якщо є, інакше HTTP) ----------
builder.Services.AddHttpClient<IOrdersClient, OrdersClient>(c => c.BaseAddress = new("https+http://orders-api"));
builder.Services.AddHttpClient<ICatalogClient, CatalogClient>(c => c.BaseAddress = new("https+http://catalog-api"));
builder.Services.AddHttpClient<IReviewsClient, ReviewsClient>(c => c.BaseAddress = new("https+http://reviews-api"));

builder.Services.AddScoped<IOrderAggregationService, OrderAggregationService>();

// camelCase — типово для MVC; null-значення не серіалізуються.
// ConfigureHttpJsonOptions діє лише на minimal API, тому для контролерів — AddJsonOptions.
builder.Services.AddControllers().AddJsonOptions(o =>
    o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);

builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
    ctx.ProblemDetails.Extensions["traceId"] = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? ctx.HttpContext.TraceIdentifier);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

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
