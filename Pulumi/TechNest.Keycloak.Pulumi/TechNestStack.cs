using Pulumi;
using Pulumi.Keycloak;
using Pulumi.Keycloak.OpenId;

namespace TechNest.KeycloakIac;

public static class TechNestStack
{
    public static IDictionary<string, object?> Create()
    {
        var config = new Config();

        // ---- Realm ----
        var realm = new Realm("technest-realm", new RealmArgs
        {
            RealmName = Matrix.RealmName,
            Enabled = true,
            DisplayName = "TechNest",
            AccessTokenLifespan = "15m",
            LoginWithEmailAllowed = true,
            RegistrationAllowed = false
        });

        // ---- Ресурс-сервери + scope-и (authorization scopes) + ресурси + клієнтські ролі ----
        var servers = new Dictionary<string, Client>();
        var authScopes = new Dictionary<string, ClientAuthorizationScope>();
        var clientRoles = new Dictionary<string, Role>();   // ключ — scope, напр. "orders:read"

        foreach (var (serverId, scopes) in Matrix.ResourceServers)
        {
            var server = ClientExtensions.CreateResourceServer(realm.Id, serverId, $"Resource server {serverId}");
            servers[serverId] = server;

            foreach (var scope in scopes)
            {
                authScopes[scope] = new ClientAuthorizationScope($"{serverId}-scope-{scope}", new ClientAuthorizationScopeArgs
                {
                    RealmId = realm.Id,
                    ResourceServerId = server.Id,
                    Name = scope
                });

                // Клієнтська роль з тим самим ім'ям: потрапляє в токен як resource_access.<server>.roles
                clientRoles[scope] = new Role($"{serverId}-role-{scope}", new RoleArgs
                {
                    RealmId = realm.Id,
                    ClientId = server.Id,
                    Name = scope
                });
            }

            // Один UMA-ресурс на сервер зі всіма його scope-ами
            _ = new ClientAuthorizationResource($"{serverId}-resource", new ClientAuthorizationResourceArgs
            {
                RealmId = realm.Id,
                ResourceServerId = server.Id,
                Name = serverId,
                Uris = [$"/api/{serverId.Replace("-api", "")}/*"],
                Scopes = scopes
            });
        }

        // ---- Реалм-ролі (composite): admin, manager, user, guest ----
        var realmRoles = new Dictionary<string, Role>();
        foreach (var (roleName, permissions) in Matrix.Roles)
        {
            realmRoles[roleName] = new Role($"realm-role-{roleName}", new RoleArgs
            {
                RealmId = realm.Id,
                Name = roleName,
                Description = $"TechNest role '{roleName}'",
                CompositeRoles = permissions.Select(p => clientRoles[p].Id).ToArray()
            });
        }

        // ---- Політики (role policy на кожну реалм-роль, на кожен ресурс-сервер) і дозволи (permission на кожен scope) ----
        foreach (var (serverId, scopes) in Matrix.ResourceServers)
        {
            var server = servers[serverId];
            var rolePolicies = new Dictionary<string, ClientRolePolicy>();

            foreach (var (roleName, role) in realmRoles)
            {
                rolePolicies[roleName] = new ClientRolePolicy($"{serverId}-policy-{roleName}", new ClientRolePolicyArgs
                {
                    RealmId = realm.Id,
                    ResourceServerId = server.Id,
                    Name = $"{roleName}-policy",
                    DecisionStrategy = "UNANIMOUS",
                    Logic = "POSITIVE",
                    Type = "role",
                    Roles = new[] { new ClientRolePolicyRoleArgs { Id = role.Id, Required = true } }
                });
            }

            foreach (var scope in scopes)
            {
                var allowed = Matrix.Roles.Where(r => r.Value.Contains(scope)).Select(r => rolePolicies[r.Key].Id).ToArray();
                _ = new ClientAuthorizationPermission($"{serverId}-permission-{scope}", new ClientAuthorizationPermissionArgs
                {
                    RealmId = realm.Id,
                    ResourceServerId = server.Id,
                    Name = $"{scope}-permission",
                    DecisionStrategy = "AFFIRMATIVE",   // достатньо однієї з дозволених ролей
                    Policies = allowed,
                    Scopes = new[] { authScopes[scope].Id },
                    Type = "scope"
                });
            }
        }

        // ---- Клієнти ----
        // Authorization Code (+ PKCE): Postman і Swagger; Client Credentials: Aggregator. Фабричні методи — ClientExtensions.
        var postman = ClientExtensions.CreateUserClient(realm.Id, "postman", ["https://oauth.pstmn.io/v1/callback"], isPublic: false);
        var swagger = ClientExtensions.CreateUserClient(realm.Id, "swagger", ["http://localhost:5000/swagger/oauth2-redirect.html"], isPublic: true);
        var aggregator = ClientExtensions.CreateServiceClient(realm.Id, "aggregator-service");

        // Default scopes, audience mappers і protocol mapper дозволів (claim authorization.permissions) — extension-методи
        foreach (var (name, client) in new[] { ("postman", postman), ("swagger", swagger) })
        {
            client.WithDefaultScopes(realm.Id, name)
                  .WithAudienceMappers(realm.Id, name, Matrix.ResourceServers.Keys)
                  .WithPermissionsMapper(realm.Id, name);
        }
        aggregator.WithDefaultScopes(realm.Id, "aggregator-service")
                  .WithAudienceMappers(realm.Id, "aggregator-service", Matrix.ResourceServers.Keys.Where(k => k != "payments-api"));

        // ---- Тестові користувачі (по одному на кожну роль) ----
        // temporaryPasswords=true — користувач змінює пароль при першому вході (рекомендація методички); за замовчуванням false,
        // щоб скрипт перевірки матриці міг отримати токен через Direct Access Grant.
        var password = config.GetSecret("testUserPassword") ?? Output.CreateSecret("Pass#12345");
        var temporaryPasswords = config.GetBoolean("temporaryPasswords") ?? false;
        foreach (var (userName, role, email) in Matrix.Users)
        {
            var user = new User($"user-{userName}", new UserArgs
            {
                RealmId = realm.Id,
                Username = userName,
                Email = email,
                EmailVerified = true,
                Enabled = true,
                FirstName = userName,
                LastName = "TechNest",
                InitialPassword = new UserInitialPasswordArgs { Value = password, Temporary = temporaryPasswords }
            });

            _ = new UserRoles($"user-roles-{userName}", new UserRolesArgs
            {
                RealmId = realm.Id,
                UserId = user.Id,
                RoleIds = new[] { realmRoles[role].Id },
                Exhaustive = true
            });
        }

        return new Dictionary<string, object?>
        {
            ["RealmUrl"] = $"http://localhost:8080/realms/{Matrix.RealmName}",
            ["AdminConsoleUrl"] = "http://localhost:8080/admin/master/console/#/" + Matrix.RealmName,
            ["TokenEndpoint"] = $"http://localhost:8080/realms/{Matrix.RealmName}/protocol/openid-connect/token",
            ["PostmanClientId"] = postman.ClientId,
            ["PostmanClientSecret"] = postman.ClientSecret,
            ["SwaggerClientId"] = swagger.ClientId,
            ["AggregatorClientId"] = aggregator.ClientId,
            ["AggregatorClientSecret"] = aggregator.ClientSecret
        };
    }
}
