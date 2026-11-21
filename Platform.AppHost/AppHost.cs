// Оркестрація платформи (.NET Aspire): один запуск (dotnet run) піднімає API разом із базою даних
// і відкриває Aspire Dashboard зі станом ресурсів.
var builder = DistributedApplication.CreateBuilder(args);

// Варіант 1 (за замовчуванням): існуюча БД з Лаб. 01.
// Рядок підключення "OrdersDb" береться з appsettings.json AppHost (секція ConnectionStrings).
var ordersDb = builder.AddConnectionString("OrdersDb");

// Варіант 2: SQL Server у контейнері (потрібен Docker Desktop). База створюється ПОРОЖНЬОЮ —
// застосуйте до неї db/p1/schema.sql, procedures.sql, seed.sql (рядок підключення видно в Dashboard).
// var ordersDb = builder.AddSqlServer("sql").WithDataVolume().AddDatabase("OrdersDb");

// Ім'я ресурсу "OrdersDb" збігається з іменем рядка підключення в Orders.Api, тому Aspire передає його
// у ConnectionStrings__OrdersDb автоматично — код Orders.Api змінювати не потрібно.
builder.AddProject<Projects.Orders_Api>("orders-api")
       .WithReference(ordersDb);

// Сервіс «Каталог» (Лаб. 03, EF Core): схему створюють міграції, тому БД CatalogDb має бути НЕ створена скриптами Лаб. 01
// (або порожня). Варіант 2: builder.AddSqlServer("sql").WithDataVolume().AddDatabase("CatalogDb") — міграції застосуються самі.
var catalogDb = builder.AddConnectionString("CatalogDb");

builder.AddProject<Projects.Catalog_Api>("catalog-api")
       .WithReference(catalogDb);

// Сервіс «Відгуки» (Лаб. 04, Clean Architecture + MongoDB). Рядок підключення "ReviewsDb" — у appsettings.json AppHost;
// Reviews.Infrastructure віддає йому пріоритет над секцією MongoDb. Варіант 2 (MongoDB у контейнері):
// var reviewsDb = builder.AddMongoDB("mongo").WithDataVolume().AddDatabase("ReviewsDb");  // потрібен пакет Aspire.Hosting.MongoDB
var reviewsDb = builder.AddConnectionString("ReviewsDb");

builder.AddProject<Projects.Reviews_WebApi>("reviews-webapi")
       .WithReference(reviewsDb);

builder.Build().Run();
