using Pulumi;
using Pulumi.Keycloak.OpenId;

namespace TechNest.KeycloakIac;

/// <summary>Фабричні методи для типових клієнтів: прибирають повторення в TechNestStack.</summary>
public static class ClientExtensions
{
    /// <summary>Ресурс-сервер (API): confidential-клієнт із увімкненими Authorization Services; токени для нього не видаються напряму.</summary>
    public static Client CreateResourceServer(Output<string> realmId, string clientId, string description) =>
        new(clientId, new ClientArgs
        {
            RealmId = realmId,
            ClientId = clientId,
            Name = clientId,
            Description = description,
            Enabled = true,
            AccessType = "CONFIDENTIAL",
            StandardFlowEnabled = false,
            DirectAccessGrantsEnabled = false,
            ServiceAccountsEnabled = true,    // потрібно для Authorization Services
            Authorization = new ClientAuthorizationArgs
            {
                PolicyEnforcementMode = "ENFORCING",
                AllowRemoteResourceManagement = true,
                KeepDefaults = false
            }
        });

    /// <summary>Клієнт для користувачів (Authorization Code + PKCE). Direct Access Grants (password) увімкнено лише для автоматичної перевірки матриці.</summary>
    public static Client CreateUserClient(Output<string> realmId, string clientId, string[] redirectUris, bool isPublic) =>
        new(clientId, new ClientArgs
        {
            RealmId = realmId,
            ClientId = clientId,
            Name = clientId,
            Enabled = true,
            AccessType = isPublic ? "PUBLIC" : "CONFIDENTIAL",
            StandardFlowEnabled = true,
            DirectAccessGrantsEnabled = true,
            PkceCodeChallengeMethod = "S256",
            ValidRedirectUris = redirectUris,
            WebOrigins = ["+"],
            FullScopeAllowed = true           // у токен потрапляють усі клієнтські ролі користувача (resource_access)
        });

    /// <summary>Default client scopes: стандартні OIDC-scopes, які клієнт отримує без явного запиту.</summary>
    public static Client WithDefaultScopes(this Client client, Output<string> realmId, string clientId)
    {
        _ = new ClientDefaultScopes($"{clientId}-default-scopes", new ClientDefaultScopesArgs
        {
            RealmId = realmId,
            ClientId = client.Id,
            DefaultScopes = ["profile", "email", "roles", "web-origins", "basic"]
        });
        return client;
    }

    /// <summary>Audience mappers: у токен додається aud для кожного ресурс-сервера, щоб мікросервіси впізнавали «свій» токен.</summary>
    public static Client WithAudienceMappers(this Client client, Output<string> realmId, string clientId, IEnumerable<string> resourceServers)
    {
        foreach (var server in resourceServers)
        {
            _ = new AudienceProtocolMapper($"{clientId}-aud-{server}", new AudienceProtocolMapperArgs
            {
                RealmId = realmId,
                ClientId = client.Id,
                Name = $"audience-{server}",
                IncludedClientAudience = server,
                AddToAccessToken = true,
                AddToIdToken = false
            });
        }
        return client;
    }

    /// <summary>
    /// Protocol mapper дозволів: клієнтські ролі користувача (composite-ролі realm-ролі розгортаються в клієнтські ролі
    /// orders:read, products:update …) потрапляють у claim <c>authorization.permissions</c> access-токена.
    /// Мікросервіси читають саме його (PermissionAuthorizationHandler).
    /// </summary>
    public static Client WithPermissionsMapper(this Client client, Output<string> realmId, string clientId)
    {
        _ = new UserClientRoleProtocolMapper($"{clientId}-permissions-mapper", new UserClientRoleProtocolMapperArgs
        {
            RealmId = realmId,
            ClientId = client.Id,
            Name = "permissions",
            ClaimName = "authorization.permissions",
            ClaimValueType = "String",
            Multivalued = true,
            AddToAccessToken = true,
            AddToIdToken = false,
            AddToUserinfo = false
        });
        return client;
    }

    /// <summary>Машинний клієнт (Client Credentials, service account).</summary>
    public static Client CreateServiceClient(Output<string> realmId, string clientId) =>
        new(clientId, new ClientArgs
        {
            RealmId = realmId,
            ClientId = clientId,
            Name = clientId,
            Enabled = true,
            AccessType = "CONFIDENTIAL",
            StandardFlowEnabled = false,
            ServiceAccountsEnabled = true
        });
}
