using System.Text.Json;
using Microsoft.AspNetCore.Authorization;

namespace Platform.Security.Authorization;

/// <summary>
/// Перевіряє permission ЛОКАЛЬНО за токеном. Джерела permissions залежно від емітента токена:
/// <list type="number">
/// <item><b>Keycloak</b>: claim <c>authorization.permissions</c> — список рядків (protocol mapper) або <c>[].scopes[]</c> (UMA/RPT) або клієнтські ролі
/// <c>resource_access.*.roles</c> (composite-ролі, які Pulumi призначає realm-ролям);</item>
/// <item><b>IdentityServer</b>: claim <c>scope</c> («orders.read» ≙ permission «orders:read»);</item>
/// <item><b>Власний Auth Service</b>: ролі <c>role</c>: Admin — усе, User — читання й створення.</item>
/// </list>
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (PermissionEvaluator.HasPermission(context.User, requirement.Permission))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}

public static class PermissionEvaluator
{
    public static bool HasPermission(System.Security.Claims.ClaimsPrincipal user, string permission) =>
        HasKeycloakPermission(user, permission)
        || HasScope(user, permission)
        || HasLocalRolePermission(user, permission);

    /// <summary>Scope «orders.read» ≙ permission «orders:read» (крапка й двокрапка рівнозначні).</summary>
    public static bool HasScope(System.Security.Claims.ClaimsPrincipal user, string scopeOrPermission)
    {
        var wanted = Normalize(scopeOrPermission);
        return user.FindAll("scope")
            .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Any(s => Normalize(s) == wanted);
    }

    private static bool HasKeycloakPermission(System.Security.Claims.ClaimsPrincipal user, string permission)
    {
        // 1) authorization.permissions[].scopes[] — токен із Authorization Services (RPT)
        var authorization = user.FindFirst("authorization")?.Value;
        if (!string.IsNullOrEmpty(authorization) && TryParse(authorization, out var doc))
        {
            using (doc)
            {
                if (doc.RootElement.TryGetProperty("permissions", out var permissions) && permissions.ValueKind == JsonValueKind.Array)
                {
                    foreach (var p in permissions.EnumerateArray())
                    {
                        // формат protocol mapper'а (Pulumi): "permissions": ["orders:read", ...]
                        if (p.ValueKind == JsonValueKind.String)
                        {
                            if (p.GetString() == permission) return true;
                            continue;
                        }

                        // формат RPT (UMA ticket grant): "permissions": [{ "rsname": "orders-api", "scopes": ["orders:read"] }]
                        if (p.ValueKind != JsonValueKind.Object ||
                            !p.TryGetProperty("scopes", out var scopes) || scopes.ValueKind != JsonValueKind.Array) continue;
                        if (scopes.EnumerateArray().Any(s => s.GetString() == permission)) return true;
                    }
                }
            }
        }

        // 2) resource_access.<клієнт>.roles[] — клієнтські ролі (composite-ролі з Pulumi розгортаються в токені)
        var resourceAccess = user.FindFirst("resource_access")?.Value;
        if (!string.IsNullOrEmpty(resourceAccess) && TryParse(resourceAccess, out var ra))
        {
            using (ra)
            {
                foreach (var client in ra.RootElement.EnumerateObject())
                {
                    if (client.Value.ValueKind != JsonValueKind.Object ||
                        !client.Value.TryGetProperty("roles", out var roles) || roles.ValueKind != JsonValueKind.Array) continue;
                    if (roles.EnumerateArray().Any(r => r.GetString() == permission)) return true;
                }
            }
        }

        return false;
    }

    private static bool HasLocalRolePermission(System.Security.Claims.ClaimsPrincipal user, string permission)
    {
        var roles = user.FindAll("role").Select(c => c.Value).ToList();
        if (roles.Contains("Admin")) return true;
        return roles.Contains("User") && (permission.EndsWith(":read") || permission.EndsWith(":create"));
    }

    private static string Normalize(string value) => value.Replace(':', '.');

    private static bool TryParse(string json, out JsonDocument doc)
    {
        try { doc = JsonDocument.Parse(json); return true; }
        catch (JsonException) { doc = null!; return false; }
    }
}
