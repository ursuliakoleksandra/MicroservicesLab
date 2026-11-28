using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Platform.Security.Authorization;

public static class AuthorizationExtensions
{
    /// <summary>
    /// Реєструє: (1) permission-політики «perm:…» для [RequirePermission] (створюються на льоту), (2) scope-політики
    /// «orders.read» тощо для M2M-викликів (Client Credentials, Частина 2) — напр. на gRPC-сервісах.
    /// </summary>
    public static IServiceCollection AddPlatformAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            foreach (var scope in ApiScopes.All)
            {
                options.AddPolicy(scope, policy => policy
                    .RequireAuthenticatedUser()
                    .RequireAssertion(ctx => PermissionEvaluator.HasScope(ctx.User, scope)));
            }
        });

        services.TryAddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        return services;
    }
}

/// <summary>Створює політику для будь-якого «perm:{permission}» — не потрібно реєструвати політику на кожен permission окремо.</summary>
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        var existing = await base.GetPolicyAsync(policyName);
        if (existing is not null) return existing;

        if (!policyName.StartsWith(PermissionPolicy.Prefix, StringComparison.Ordinal)) return null;

        return new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(policyName[PermissionPolicy.Prefix.Length..]))
            .Build();
    }
}
