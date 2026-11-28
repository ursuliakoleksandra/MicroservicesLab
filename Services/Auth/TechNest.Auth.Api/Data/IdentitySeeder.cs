using TechNest.Auth.Api.Controllers;
using Microsoft.AspNetCore.Identity;

namespace TechNest.Auth.Api.Data;

/// <summary>Ролі User і Admin створюються під час старту; в Development — ще й тестовий адміністратор (Seed:*).</summary>
public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration config)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("IdentitySeeder");

        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
                logger.LogInformation("Role {Role} created", role);
            }
        }

        var adminEmail = config["Seed:AdminEmail"];
        var adminPassword = config["Seed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
        {
            logger.LogInformation("Seed:AdminEmail / Seed:AdminPassword are not set — default administrator was not created");
            return;
        }

        if (await userManager.FindByEmailAsync(adminEmail) is not null) return;

        var admin = new ApplicationUser
        {
            UserName = config["Seed:AdminUserName"] ?? "admin",
            Email = adminEmail,
            EmailConfirmed = true,
            FullName = "Administrator"
        };
        var result = await userManager.CreateAsync(admin, adminPassword);
        if (!result.Succeeded)
        {
            logger.LogError("Administrator was not created: {Errors}", string.Join("; ", result.Errors.Select(e => e.Description)));
            return;
        }

        await userManager.AddToRolesAsync(admin, Roles.All);
        logger.LogInformation("Administrator {Email} created", adminEmail);
    }
}
