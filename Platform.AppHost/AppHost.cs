// Оркестрація платформи TechNest (.NET Aspire): одна команда `dotnet run --project Platform.AppHost` піднімає
// контейнери SQL Server і MongoDB, три мікросервіси, Aggregator і Gateway та відкриває Aspire Dashboard.
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

// Імена баз збігаються з іменами рядків підключення, які сервіси вже читають (ConnectionStrings:OrdersDb ...),
// тому Aspire передає їх через WithReference — код сервісів не змінюється.

// ---------- одноразова ініціалізація OrdersDb: скрипти db/p1 (схема, процедури, seed) ----------
var ordersDbInit = builder.AddProject<Projects.Platform_DbInit>("orders-db-init")
    .WithReference(ordersDb).WaitFor(ordersDb);

// ---------- мікросервіси (ім'я ресурсу = логічна адреса для service discovery: http://orders-api) ----------
var orders = builder.AddProject<Projects.Orders_Api>("orders-api")
    .WithReference(ordersDb).WaitFor(ordersDb)
    .WaitForCompletion(ordersDbInit);                        // стартує лише після застосування схеми

var catalog = builder.AddProject<Projects.Catalog_Api>("catalog-api")
    .WithReference(catalogDb).WaitFor(catalogDb);

var reviews = builder.AddProject<Projects.Reviews_WebApi>("reviews-api")
    .WithReference(reviewsDb).WaitFor(reviewsDb);

// ---------- Aggregator: опитує три сервіси (WithReference = service discovery, WaitFor = порядок запуску) ----------
var aggregator = builder.AddProject<Projects.Platform_Aggregator>("aggregator")
    .WithReference(orders).WithReference(catalog).WithReference(reviews)
    .WaitFor(orders).WaitFor(catalog).WaitFor(reviews);

// ---------- Gateway: єдина точка входу з фіксованим портом 5000 ----------
builder.AddProject<Projects.Platform_ApiGateway>("gateway")
    .WithHttpEndpoint(port: 5000, name: "public")
    .WithExternalHttpEndpoints()
    .WithReference(orders).WithReference(catalog).WithReference(reviews).WithReference(aggregator)
    .WaitFor(aggregator);

builder.Build().Run();
