using Duende.IdentityServer.EntityFramework.DbContexts;
using Duende.IdentityServer.EntityFramework.Mappers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace TechNest.IdentityServer.Data;

public static class IdentityServerSeeder
{
    /// <summary>Міграції трьох контекстів, початкова конфігурація й тестові користувачі (Development).</summary>
    public static async Task SeedAsync(IServiceProvider services, IConfiguration config)
    {
        // Три контексти в одній БД: PersistedGrantDbContext, ApplicationDbContext (Identity), ConfigurationDbContext.
        // Якщо міграції згенеровано (dotnet ef migrations add, див. scripts/add-migrations.ps1) — застосовуються вони;
        // інакше (перший запуск без міграцій) таблиці створюються з моделі.
        await DatabaseInitializer.ApplyAsync(services.GetRequiredService<PersistedGrantDbContext>());
        await DatabaseInitializer.ApplyAsync(services.GetRequiredService<ApplicationDbContext>());
        var cfg = services.GetRequiredService<ConfigurationDbContext>();
        await DatabaseInitializer.ApplyAsync(cfg);

        // Clients / scopes додаються лише коли таблиця порожня; для оновлення — очистити БД IdentityServerDb
        if (!await cfg.Clients.AnyAsync())
        {
            foreach (var c in IdentityServerConfig.Clients(config)) cfg.Clients.Add(c.ToEntity());
            await cfg.SaveChangesAsync();
        }
        if (!await cfg.IdentityResources.AnyAsync())
        {
            foreach (var r in IdentityServerConfig.IdentityResources) cfg.IdentityResources.Add(r.ToEntity());
            await cfg.SaveChangesAsync();
        }
        if (!await cfg.ApiScopes.AnyAsync())
        {
            foreach (var s in IdentityServerConfig.ApiScopes) cfg.ApiScopes.Add(s.ToEntity());
            await cfg.SaveChangesAsync();
        }
        if (!await cfg.ApiResources.AnyAsync())
        {
            foreach (var r in IdentityServerConfig.ApiResources) cfg.ApiResources.Add(r.ToEntity());
            await cfg.SaveChangesAsync();
        }

        var password = config["TestUsers:Password"];
        if (string.IsNullOrWhiteSpace(password)) return;   // користувачі створюються лише якщо пароль заданий (Development)

        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        await EnsureUserAsync(users, "alice", "alice@technest.local", "Alice Admin", password, "Admin");
        await EnsureUserAsync(users, "bob", "bob@technest.local", "Bob User", password, "User");
    }

    private static async Task EnsureUserAsync(UserManager<ApplicationUser> users, string name, string email, string fullName, string password, string role)
    {
        if (await users.FindByNameAsync(name) is not null) return;
        var user = new ApplicationUser { UserName = name, Email = email, EmailConfirmed = true, FullName = fullName };
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        await users.AddClaimsAsync(user, [new Claim("role", role), new Claim("name", fullName)]);
    }
}
