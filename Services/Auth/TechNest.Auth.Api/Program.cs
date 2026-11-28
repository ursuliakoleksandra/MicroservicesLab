using TechNest.Auth.Api.Data;
using TechNest.Auth.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Platform.Security.Authentication;
using Platform.Security.Authorization;
using Platform.ServiceDefaults.Correlation;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();   // Serilog, OpenTelemetry, health checks, service discovery

// ---- Окрема база IdentityDb (рядок підключення від AppHost: ConnectionStrings:IdentityDb) ----
var connectionString = builder.Configuration.GetConnectionString("IdentityDb")
    ?? throw new InvalidOperationException("Connection string 'IdentityDb' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseSqlServer(connectionString));

// ---- ASP.NET Identity: вимоги до пароля, lockout, унікальний email ----
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = true;

        options.Lockout.MaxFailedAccessAttempts = 5;                    // 5 невдалих спроб → блокування
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;

        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedEmail = false;                    // перевіряється явно в /auth/login
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// Токени підтвердження email і скидання пароля мають обмежений термін дії (одноразовість — через security stamp)
builder.Services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromHours(2));

// ---- JWT (Issuer/Audience/SecretKey — спільні для Gateway і мікросервісів) ----
builder.AddPlatformAuthentication();      // Auth Service перевіряє власні токени (схема Local)
builder.Services.AddPlatformAuthorization();

builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<RefreshTokenService>();
builder.Services.AddSingleton<IAuthEmailSender, LoggingEmailSender>();

builder.Services.AddHealthChecks()
    .AddSqlServer(connectionString, name: "identity-db", failureStatus: HealthStatus.Unhealthy,
        tags: ["database", "sql", "ready"], timeout: TimeSpan.FromSeconds(5));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();

var app = builder.Build();

// ---- Міграції (створюють AspNetUsers, AspNetRoles, AspNetUserRoles… + RefreshTokens) і ролі User/Admin ----
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await DatabaseInitializer.ApplyAsync(db);      // міграції (якщо згенеровані) або створення таблиць із моделі
    await IdentitySeeder.SeedAsync(scope.ServiceProvider, app.Configuration);
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapDefaultEndpoints();   // /health/live, /health/ready

app.Run();
