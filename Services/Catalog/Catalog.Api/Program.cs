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
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("CatalogDb")
    ?? throw new InvalidOperationException("Connection string 'CatalogDb' is missing");

// ---------- Serilog: структуровані логи (властивості як ключі), sink-и: console + file (див. appsettings.json) ----------
builder.Host.UseSerilog((ctx, cfg) =>
{
    cfg.ReadFrom.Configuration(ctx.Configuration)
       .Enrich.FromLogContext()                       // CorrelationId, RequestPath, UserId — з RequestLogContextMiddleware
       .WriteTo.Console(outputTemplate:
           "[{Timestamp:HH:mm:ss} {Level:u3}] ({CorrelationId}) {RequestPath} {Message:lj}{NewLine}{Exception}");

    // SQL-команди EF Core в лозі: eager = 1 запит, explicit = 2 запити
    if (ctx.Configuration.GetValue("Diagnostics:LogSqlCommands", true))
        cfg.MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Information);
});

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
builder.Services.AddControllers();                    // DataAnnotations -> автоматичний 400 ValidationProblemDetails

builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
    ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

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

app.UseMiddleware<RequestLogContextMiddleware>();
app.UseSerilogRequestLogging();
app.UseExceptionHandler();                            // глобальна обробка: виняток -> ProblemDetails (логується один раз)

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
}

app.MapControllers();
app.Run();

public partial class Program;                         // для WebApplicationFactory (API-тести)
