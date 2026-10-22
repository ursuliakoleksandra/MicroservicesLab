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

builder.Build().Run();
