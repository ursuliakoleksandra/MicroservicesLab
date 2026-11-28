using TechNest.IdentityServer.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

var connectionString = builder.Configuration.GetConnectionString("IdentityServerDb")
    ?? throw new InvalidOperationException("Connection string 'IdentityServerDb' not found.");
var migrationsAssembly = typeof(Program).Assembly.GetName().Name;

builder.Services.AddControllersWithViews();

// Користувачі — ASP.NET Identity у власній БД IdentityServerDb
builder.Services.AddDbContext<ApplicationDbContext>(o => o.UseSqlServer(connectionString, b => b.MigrationsAssembly(migrationsAssembly)));
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(o =>
    {
        o.Password.RequiredLength = 8;
        o.Lockout.MaxFailedAccessAttempts = 5;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(o => o.LoginPath = "/Account/Login");

// IdentityServer: конфігурація (clients/scopes/resources) і operational-дані (grants, keys) — у БД
builder.Services.AddIdentityServer(options =>
    {
        options.IssuerUri = builder.Configuration["IdentityServer:IssuerUri"];   // фіксований iss: http://localhost:5400
        options.EmitStaticAudienceClaim = false;                                 // aud беремо з ApiResource
        options.Events.RaiseSuccessEvents = true;
        options.Events.RaiseFailureEvents = true;
        options.Events.RaiseErrorEvents = true;
        options.KeyManagement.Enabled = true;                                    // автоматичні signing keys (RS256) у БД
    })
    .AddAspNetIdentity<ApplicationUser>()
    .AddConfigurationStore(o => o.ConfigureDbContext = b => b.UseSqlServer(connectionString, s => s.MigrationsAssembly(migrationsAssembly)))
    .AddOperationalStore(o =>
    {
        o.ConfigureDbContext = b => b.UseSqlServer(connectionString, s => s.MigrationsAssembly(migrationsAssembly));
        o.EnableTokenCleanup = true;
    });

builder.Services.AddHealthChecks()
    .AddSqlServer(connectionString, name: "identityserver-db", failureStatus: HealthStatus.Unhealthy,
        tags: ["database", "sql", "ready"], timeout: TimeSpan.FromSeconds(5));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
    await IdentityServerSeeder.SeedAsync(scope.ServiceProvider, app.Configuration);

app.UseStaticFiles();
app.UseRouting();
app.UseIdentityServer();     // middleware: /connect/*, /.well-known/*; включає UseAuthentication
app.UseAuthorization();
app.MapDefaultControllerRoute();
app.MapDefaultEndpoints();

app.Run();
