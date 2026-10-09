var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql");
var ordersDb = sql.AddDatabase("OrdersDb");

builder.AddProject<Projects.Orders_Api>("orders-api")
    .WithReference(ordersDb)
    .WaitFor(ordersDb);

builder.Build().Run();