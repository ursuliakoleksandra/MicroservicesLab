using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Platform.Security;

namespace TechNest.IdentityServer.Data;

/// <summary>Статична конфігурація, яка записується в ConfigurationDbContext при старті (Clients, Scopes, ApiResources, IdentityResources).</summary>
public static class IdentityServerConfig
{
    public static IEnumerable<IdentityResource> IdentityResources =>
    [
        new IdentityResources.OpenId(),
        new IdentityResources.Profile(),
        new IdentityResources.Email(),
        new IdentityResource("roles", "User roles", ["role"])
    ];

    // Scope-и кожного API: {ресурс}.{дія}
    public static IEnumerable<ApiScope> ApiScopes => Platform.Security.ApiScopes.All.Select(s => new ApiScope(s, s));

    // ApiResource групує scope-и й задає audience (aud) токена
    public static IEnumerable<ApiResource> ApiResources =>
    [
        Resource(ApiResourceNames.Orders, "Orders API", Platform.Security.ApiScopes.All.Where(s => s.StartsWith("orders."))),
        Resource(ApiResourceNames.Products, "Products API", Platform.Security.ApiScopes.All.Where(s => s.StartsWith("products."))),
        Resource(ApiResourceNames.Reviews, "Reviews API", Platform.Security.ApiScopes.All.Where(s => s.StartsWith("reviews."))),
        Resource(ApiResourceNames.Payments, "Payments API", Platform.Security.ApiScopes.All.Where(s => s.StartsWith("payments.")))
    ];

    private static ApiResource Resource(string name, string display, IEnumerable<string> scopes) =>
        new(name, display, ["role"]) { Scopes = scopes.ToList() };

    public static IEnumerable<Client> Clients(IConfiguration config)
    {
        var postmanSecret = config["Clients:PostmanSecret"] ?? throw new InvalidOperationException("Clients:PostmanSecret is not set.");
        var aggregatorSecret = config["Clients:AggregatorSecret"] ?? throw new InvalidOperationException("Clients:AggregatorSecret is not set.");

        var userScopes = new[]
        {
            IdentityServerConstants.StandardScopes.OpenId, IdentityServerConstants.StandardScopes.Profile,
            IdentityServerConstants.StandardScopes.Email, "roles"
        }.Concat(Platform.Security.ApiScopes.All).ToArray();      // користувацький токен може отримати будь-які scopes API (клієнт запитує потрібні)

        return
        [
            // Authorization Code + PKCE: Postman (confidential client із секретом)
            new Client
            {
                ClientId = "postman",
                ClientName = "Postman",
                ClientSecrets = { new Secret(postmanSecret.Sha256()) },
                AllowedGrantTypes = GrantTypes.Code,
                RequirePkce = true,
                RedirectUris = { "https://oauth.pstmn.io/v1/callback" },
                PostLogoutRedirectUris = { "https://oauth.pstmn.io/v1/callback" },
                AllowedScopes = userScopes,
                AllowOfflineAccess = true,                  // refresh token
                AccessTokenLifetime = 900,
                AlwaysIncludeUserClaimsInIdToken = false
            },
            // Swagger UI: публічний клієнт (SPA) — PKCE без секрету
            new Client
            {
                ClientId = "swagger",
                ClientName = "Swagger UI",
                RequireClientSecret = false,
                AllowedGrantTypes = GrantTypes.Code,
                RequirePkce = true,
                RedirectUris = { "http://localhost:5000/swagger/oauth2-redirect.html" },
                AllowedCorsOrigins = { "http://localhost:5000" },
                AllowedScopes = userScopes
            },
            // Client Credentials: машина-машина (Aggregator → gRPC-сервіси), без користувача — у токені немає sub
            new Client
            {
                ClientId = "aggregator_service",
                ClientName = "Aggregator Service",
                ClientSecrets = { new Secret(aggregatorSecret.Sha256()) },
                AllowedGrantTypes = GrantTypes.ClientCredentials,
                AllowedScopes = { "orders.read", "products.read", "reviews.read" },   // лише читання: Aggregator нічого не змінює
                AccessTokenLifetime = 3600
            }
        ];
    }
}
