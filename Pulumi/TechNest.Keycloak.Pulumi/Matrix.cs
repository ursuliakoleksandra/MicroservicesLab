namespace TechNest.KeycloakIac;

/// <summary>Єдине джерело правди про дозволи: ресурс-сервери (Keycloak clients), їхні scope-и та матриця «роль → дозволи».</summary>
public static class Matrix
{
    public const string RealmName = "TechNestRealm";

    // Ресурс-сервер (client) → scope-и (UMA authorization scopes = client roles = permissions у токені)
    public static readonly IReadOnlyDictionary<string, string[]> ResourceServers = new Dictionary<string, string[]>
    {
        ["orders-api"] = ["orders:read", "orders:create", "orders:update", "orders:delete", "orders:approve"],
        ["products-api"] = ["products:read", "products:create", "products:update", "products:delete", "products:publish"],
        ["payments-api"] = ["payments:read", "payments:process", "payments:refund"]
    };

    // Реалм-роль → дозволи (scope-и). Composite-роль складається з відповідних клієнтських ролей.
    public static readonly IReadOnlyDictionary<string, string[]> Roles = new Dictionary<string, string[]>
    {
        ["admin"] = ResourceServers.Values.SelectMany(s => s).ToArray(),
        // таблиця 7: manager — read, update, approve (orders) і read, update, publish (products); payments — жодного дозволу
        ["manager"] = ["orders:read", "orders:update", "orders:approve", "products:read", "products:update", "products:publish"],
        // user — read, create (orders) і лише read (products); guest — лише read (products); payments-api бачить ЛИШЕ admin
        ["user"] = ["orders:read", "orders:create", "products:read"],
        ["guest"] = ["products:read"]
    };

    // Тестові користувачі: логін → (роль, email). Початкові паролі тимчасові (змінюються при першому вході).
    public static readonly (string UserName, string Role, string Email)[] Users =
    [
        ("alice", "admin", "alice@technest.local"),
        ("bob", "manager", "bob@technest.local"),
        ("carol", "user", "carol@technest.local"),
        ("dave", "guest", "dave@technest.local")
    ];

    public static string ServerOf(string scope) =>
        ResourceServers.First(kv => kv.Value.Contains(scope)).Key;
}
