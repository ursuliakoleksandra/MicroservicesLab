using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TechNest.IdentityServer.Data;

/// <summary>Користувачі IdentityServer (ASP.NET Identity) — окрема база IdentityServerDb.</summary>
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options);

// Design-time фабрики: потрібні лише для `dotnet ef migrations add` (рядок підключення не використовується під час міграції)
public static class DesignTime
{
    public const string ConnectionString = "Server=localhost;Database=IdentityServerDb;Trusted_Connection=True;TrustServerCertificate=True";
    public const string Assembly = "TechNest.IdentityServer";
}

public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(DesignTime.ConnectionString).Options);
}

public class PersistedGrantDbContextFactory : IDesignTimeDbContextFactory<Duende.IdentityServer.EntityFramework.DbContexts.PersistedGrantDbContext>
{
    public Duende.IdentityServer.EntityFramework.DbContexts.PersistedGrantDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<Duende.IdentityServer.EntityFramework.DbContexts.PersistedGrantDbContext>()
            .UseSqlServer(DesignTime.ConnectionString, b => b.MigrationsAssembly(DesignTime.Assembly)).Options);
}

public class ConfigurationDbContextFactory : IDesignTimeDbContextFactory<Duende.IdentityServer.EntityFramework.DbContexts.ConfigurationDbContext>
{
    public Duende.IdentityServer.EntityFramework.DbContexts.ConfigurationDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<Duende.IdentityServer.EntityFramework.DbContexts.ConfigurationDbContext>()
            .UseSqlServer(DesignTime.ConnectionString, b => b.MigrationsAssembly(DesignTime.Assembly)).Options);
}
