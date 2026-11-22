// Оркестрація платформи TechNest (.NET Aspire): одна команда `dotnet run --project Platform.AppHost` піднімає
// контейнери SQL Server, MongoDB і Redis, три мікросервіси, Aggregator і Gateway та відкриває Aspire Dashboard.
// AppHost не містить бізнес-логіки — лише опис ресурсів і залежностей між ними.
var builder = DistributedApplication.CreateBuilder(args);

// ---------- бази даних у контейнерах (Docker Desktop має бути запущений) ----------
// Пароль — параметр-секрет, а не рядок у коді: dotnet user-secrets set "Parameters:sql-password" "<пароль>"
// (або його запитає Dashboard під час першого запуску).
var sqlPassword = builder.AddParameter("sql-password", secret: true);

var sql = builder.AddSqlServer("sql", sqlPassword)
                 .WithDataVolume();                          // дані переживають перезапуск
var ordersDb  = sql.AddDatabase("OrdersDb");                 // Лаб. 02 (ADO.NET + Dapper)
var catalogDb = sql.AddDatabase("CatalogDb");                // Лаб. 03 (EF Core; схему створюють міграції при старті)

var mongo = builder.AddMongoDB("mongo")
                   .WithDataVolume();                        // окремий volume
var reviewsDb = mongo.AddDatabase("ReviewsDb");              // Лаб. 04 (індекси й сідінг створює сам сервіс)

// Лаб. 06: Redis — розподілений кеш (L2) для Каталогу й кеш агрегованих відповідей Aggregator.
// Ім'я ресурсу "redis" = ім'я в AddRedisDistributedCache("redis"); рядок підключення передається як ConnectionStrings:redis.
var redis = builder.AddRedis("redis")
                   .WithDataVolume();                        // кеш переживає перезапуск Redis

// Імена баз збігаються з іменами рядків підключення, які сервіси вже читають (ConnectionStrings:OrdersDb ...),
// тому Aspire передає їх через WithReference — код сервісів не змінюється.

// ---------- одноразова ініціалізація OrdersDb: скрипти db/p1 (схема, процедури, seed) ----------
var ordersDbInit = builder.AddProject<Projects.Platform_DbInit>("orders-db-init")
    .WithReference(ordersDb).WaitFor(ordersDb);

// ---------- мікросервіси (ім'я ресурсу = логічна адреса для service discovery: https://orders-api) ----------
// Профіль запуску "https": сервіс слухає і HTTP (REST через Gateway), і HTTPS (gRPC від Aggregator, HTTP/2 через ALPN).
var orders = builder.AddProject<Projects.Orders_Api>("orders-api", launchProfileName: "https")
    .WithReference(ordersDb).WaitFor(ordersDb)
    .WaitForCompletion(ordersDbInit);                        // стартує лише після застосування схеми

var catalog = builder.AddProject<Projects.Catalog_Api>("catalog-api", launchProfileName: "https")
    .WithReference(catalogDb).WaitFor(catalogDb)
    .WithReference(redis).WaitFor(redis);                    // L2-кеш

var reviews = builder.AddProject<Projects.Reviews_WebApi>("reviews-api", launchProfileName: "https")
    .WithReference(reviewsDb).WaitFor(reviewsDb);

// ---------- Aggregator: gRPC-клієнт сервісів + кеш агрегованих відповідей у Redis ----------
// Транспорт можна перемкнути для порівняння затримок: dotnet run --project Platform.AppHost -- --Aggregator:Transport=Http
var aggregator = builder.AddProject<Projects.Platform_Aggregator>("aggregator")
    .WithEnvironment("Aggregator__Transport", builder.Configuration["Aggregator:Transport"] ?? "Grpc")
    .WithReference(orders).WithReference(catalog).WithReference(reviews)
    .WithReference(redis)
    .WaitFor(orders).WaitFor(catalog).WaitFor(reviews).WaitFor(redis);

// ---------- Gateway: єдина точка входу з фіксованим портом 5000 ----------
builder.AddProject<Projects.Platform_ApiGateway>("gateway")
    .WithHttpEndpoint(port: 5000, name: "public")
    .WithExternalHttpEndpoints()
    .WithReference(orders).WithReference(catalog).WithReference(reviews).WithReference(aggregator)
    .WaitFor(aggregator);

builder.Build().Run();
