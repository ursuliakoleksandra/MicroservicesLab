using Microsoft.AspNetCore.Authorization;

namespace Platform.Security.Authorization;

/// <summary>Вимога: у токені користувача/сервісу має бути конкретний permission (напр. «orders:read»).</summary>
public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

/// <summary>
/// Атрибут для КОЖНОГО ендпоінта: <c>[RequirePermission(Permissions.Orders.Read)]</c>. Рішення приймає сам мікросервіс
/// локально за токеном — без звернення до Identity Provider при кожному запиті.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequirePermissionAttribute(string permission) : AuthorizeAttribute(PermissionPolicy.Prefix + permission)
{
    public string Permission { get; } = permission;
}

public static class PermissionPolicy
{
    public const string Prefix = "perm:";
}
