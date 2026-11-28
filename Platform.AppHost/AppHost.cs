// Оркестрація платформи TechNest (.NET Aspire): одна команда `dotnet run --project Platform.AppHost` піднімає
// контейнери SQL Server, MongoDB, Redis і RabbitMQ, три мікросервіси, Aggregator і Gateway та відкриває Aspire Dashboard.
// AppHost не містить бізнес-логіки — лише опис ресурсів і залежностей між ними.
var builder = DistributedApplication.CreateBuilder(args);

// ---------- бази даних у контейнерах (Docker Desktop має бути запущений) ----------
// Пароль — параметр-секрет, а не рядок у коді: dotnet user-secrets set "Parameters:sql-password" "<пароль>"
// (або його запитає Dashboard під час першого запуску).
var sqlPassword = builder.AddParameter("sql-password", secret: true);

var sql = builder.AddSqlServer("sql", sqlPassword)
                 .WithDataVolume();                          // дані переживають перезапуск
var ordersDb  = sql.AddDatabase("OrdersDb");                 // Лаб. 02 (ADO.NET + Dapper)
var identityDb = sql.AddDatabase("IdentityDb");   // Лаб. 09: користувачі, ролі та refresh-токени Auth Service (ASP.NET Identity)
var catalogDb = sql.AddDatabase("CatalogDb");                // Лаб. 03 (EF Core; схему створюють міграції при старті)

// Лаб. 09: окремий SQL Server для IdentityServer (користувачі + Configuration/Operational store) — ізольований від Auth Service
var identityServerSql = builder.AddSqlServer("identityserver-sql", sqlPassword)
    .WithDataVolume("technest-identityserver-sql-data")
    .WithLifetime(ContainerLifetime.Persistent);
var identityServerDb = identityServerSql.AddDatabase("IdentityServerDb");

var mongo = builder.AddMongoDB("mongo")
                   .WithDataVolume();                        // окремий volume
var reviewsDb = mongo.AddDatabase("ReviewsDb");              // Лаб. 04 (індекси й сідінг створює сам сервіс)

// Лаб. 06: Redis — розподілений кеш (L2) для Каталогу й кеш агрегованих відповідей Aggregator.
// Ім'я ресурсу "redis" = ім'я в AddRedisDistributedCache("redis"); рядок підключення передається як ConnectionStrings:redis.
var redis = builder.AddRedis("redis")
                   .WithDataVolume();                        // кеш переживає перезапуск Redis

// Лаб. 08: RabbitMQ — брокер повідомлень. WithDataVolume: черги й повідомлення переживають перезапуск контейнера;
// WithManagementPlugin: веб-інтерфейс керування (Management UI) — exchanges, queues, bindings, DLQ (кнопка в Dashboard).
// Ім'я "messaging" = AddRabbitMQClient("messaging") у Platform.Messaging; рядок підключення передається як ConnectionStrings:messaging.
var messaging = builder.AddRabbitMQ("messaging")
                       .WithDataVolume()
                       .WithManagementPlugin();

// Лаб. 09, ч. 3 (траєкторія А): Keycloak як контейнер Aspire (AddKeycloak) з томом даних. Realm TechNestRealm створює Pulumi-проєкт
// Pulumi/TechNest.Keycloak.Pulumi (команда `pulumi up`). Пароль адміністратора — секретний параметр (user-secrets), бо з постійним томом
// випадково згенерований пароль не збігся б зі збереженим: dotnet user-secrets set "Parameters:keycloak-admin-password" "<пароль>".
var keycloakAdminPassword = builder.AddParameter("keycloak-admin-password", secret: true);
var keycloak = builder.AddKeycloak("keycloak", port: 8080, adminPassword: keycloakAdminPassword)
    .WithDataVolume("technest-keycloak-data")
    .WithLifetime(ContainerLifetime.Persistent);

// Лаб. 09: параметри безпеки. Секрет підпису локальних JWT (HS256, ≥ 32 символи): dotnet user-secrets set "Parameters:jwt-secret" "<...>"
var jwtSecret = builder.AddParameter("jwt-secret", secret: true);
var identityServerClientSecret = builder.AddParameter("aggregator-client-secret", secret: true);
const string identityServerAuthority = "http://localhost:5400";
const string keycloakAuthority = "http://localhost:8080/realms/TechNestRealm";

// Спільні налаштування перевірки JWT для всіх ресурсів, що приймають токени (Local / IdentityServer / Keycloak)
IResourceBuilder<T> WithAuth<T>(IResourceBuilder<T> resource) where T : IResourceWithEnvironment =>
    resource
        .WithEnvironment("Jwt__Issuer", "technest-auth")
        .WithEnvironment("Jwt__Audience", "technest-api")
        .WithEnvironment("Jwt__SecretKey", jwtSecret)
        .WithEnvironment("IdentityServer__Authority", identityServerAuthority)
        .WithEnvironment("Keycloak__Authority", keycloakAuthority);

// Імена баз збігаються з іменами рядків підключення, які сервіси вже читають (ConnectionStrings:OrdersDb ...),
// тому Aspire передає їх через WithReference — код сервісів не змінюється.

// ---------- одноразова ініціалізація OrdersDb: скрипти db/p1 (схема, процедури, seed) ----------
var ordersDbInit = builder.AddProject<Projects.Platform_DbInit>("orders-db-init")
    .WithReference(ordersDb).WaitFor(ordersDb);

// ---------- Лаб. 09: Auth Service (Identity + JWT) і IdentityServer (OAuth 2.0 / OIDC) ----------
var authApi = WithAuth(builder.AddProject<Projects.TechNest_Auth_Api>("auth-api")
    .WithReference(identityDb).WaitFor(identityDb)
    .WithHttpHealthCheck("/health/ready"));

builder.AddProject<Projects.TechNest_IdentityServer>("identityserver")
    .WithReference(identityServerDb).WaitFor(identityServerDb)
    .WithEnvironment("Clients__AggregatorSecret", identityServerClientSecret)
    .WithHttpHealthCheck("/health/ready");

// ---------- мікросервіси (ім'я ресурсу = логічна адреса для service discovery: https://orders-api) ----------
// Профіль запуску "https": сервіс слухає і HTTP (REST через Gateway), і HTTPS (gRPC від Aggregator, HTTP/2 через ALPN).
var orders = WithAuth(builder.AddProject<Projects.Orders_Api>("orders-api", launchProfileName: "https")
    .WithReference(ordersDb).WaitFor(ordersDb)
    .WithReference(messaging).WaitFor(messaging)             // Лаб. 08: споживач черги orders.catalog-sync
    .WaitForCompletion(ordersDbInit)                         // стартує лише після застосування схеми
    .WithHttpHealthCheck("/health", endpointName: "http"));   // Лаб. 07: Dashboard показує Healthy/Degraded/Unhealthy; WaitFor чекає Healthy

var catalog = WithAuth(builder.AddProject<Projects.Catalog_Api>("catalog-api", launchProfileName: "https")
    .WithReference(catalogDb).WaitFor(catalogDb)
    .WithReference(redis).WaitFor(redis)                     // L2-кеш
    .WithReference(messaging).WaitFor(messaging)             // Лаб. 08: publisher подій Каталогу
    .WithHttpHealthCheck("/health", endpointName: "http"));

var reviews = WithAuth(builder.AddProject<Projects.Reviews_WebApi>("reviews-api", launchProfileName: "https")
    .WithReference(reviewsDb).WaitFor(reviewsDb)
    .WithReference(messaging).WaitFor(messaging)             // Лаб. 08: споживач черги reviews.catalog-sync
    .WithHttpHealthCheck("/health", endpointName: "http"));

// Лаб. 09: заглушка платіжного сервісу (ресурс payments-api у Keycloak; доступ лише admin)
var paymentsApi = WithAuth(builder.AddProject<Projects.TechNest_Payments_Api>("payments-api")
    .WithHttpHealthCheck("/health/ready"));

// ---------- Aggregator: gRPC-клієнт сервісів + кеш агрегованих відповідей у Redis ----------
// Транспорт можна перемкнути для порівняння затримок: dotnet run --project Platform.AppHost -- --Aggregator:Transport=Http
var aggregator = WithAuth(builder.AddProject<Projects.Platform_Aggregator>("aggregator")
    .WithEnvironment("Aggregator__Transport", builder.Configuration["Aggregator:Transport"] ?? "Grpc")
    .WithReference(orders).WithReference(catalog).WithReference(reviews)
    .WithReference(redis)
    .WaitFor(orders).WaitFor(catalog).WaitFor(reviews).WaitFor(redis)   // WaitFor тепер чекає Healthy (є WithHttpHealthCheck), а не лише Running
    // Client Credentials: Aggregator отримує токен в IdentityServer (без користувача) для викликів downstream-сервісів
    .WithEnvironment("ClientCredentials__Authority", identityServerAuthority)
    .WithEnvironment("ClientCredentials__ClientId", "aggregator_service")
    .WithEnvironment("ClientCredentials__ClientSecret", identityServerClientSecret)
    .WithEnvironment("ClientCredentials__Scope", "orders.read products.read reviews.read")
    .WithHttpHealthCheck("/health"));

// ---------- Gateway: єдина точка входу з фіксованим портом 5000 ----------
WithAuth(builder.AddProject<Projects.Platform_ApiGateway>("gateway")
    .WithHttpEndpoint(port: 5000, name: "public")
    .WithExternalHttpEndpoints()
    .WithReference(orders).WithReference(catalog).WithReference(reviews).WithReference(aggregator)
    .WithReference(authApi).WithReference(paymentsApi)       // Лаб. 09: маршрути /auth, /users, /api/payments
    .WaitFor(aggregator)
    .WithHttpHealthCheck("/health", endpointName: "public"));

builder.Build().Run();
