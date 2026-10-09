using Dapper;
using Microsoft.Data.SqlClient;

namespace Orders.Dal;

public static class DbInitializer
{
    public static async Task InitializeAsync(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        var databaseName = builder.InitialCatalog;

        // 1. Створюємо БД, якщо її немає (ім'я передається параметром)
        builder.InitialCatalog = "master";
        await using (var masterConnection = new SqlConnection(builder.ConnectionString))
        {
            await masterConnection.OpenAsync();
            await masterConnection.ExecuteAsync(@"
                IF DB_ID(@Name) IS NULL
                BEGIN
                    DECLARE @sql NVARCHAR(MAX) = N'CREATE DATABASE ' + QUOTENAME(@Name);
                    EXEC (@sql);
                END", new { Name = databaseName });
        }

        // 2. Підключаємося до OrdersDb
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        // 3. Таблиці (створюються лише якщо відсутні)
        await connection.ExecuteAsync(@"
            IF OBJECT_ID(N'dbo.Customers', N'U') IS NULL
            CREATE TABLE dbo.Customers (
                Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Customers PRIMARY KEY,
                Name NVARCHAR(100) NOT NULL,
                Email NVARCHAR(100) NOT NULL
            );

            IF OBJECT_ID(N'dbo.Products', N'U') IS NULL
            CREATE TABLE dbo.Products (
                Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Products PRIMARY KEY,
                Name NVARCHAR(100) NOT NULL,
                Price DECIMAL(18,2) NOT NULL
            );

            IF OBJECT_ID(N'dbo.Orders', N'U') IS NULL
            CREATE TABLE dbo.Orders (
                Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Orders PRIMARY KEY,
                CustomerId UNIQUEIDENTIFIER NOT NULL,
                OrderDate DATETIME2 NOT NULL,
                TotalAmount DECIMAL(18,2) NOT NULL,
                Status INT NOT NULL,
                CONSTRAINT FK_Orders_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers(Id)
            );

            IF OBJECT_ID(N'dbo.OrderItems', N'U') IS NULL
            CREATE TABLE dbo.OrderItems (
                Id UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_OrderItems PRIMARY KEY,
                OrderId UNIQUEIDENTIFIER NOT NULL,
                ProductId UNIQUEIDENTIFIER NOT NULL,
                Quantity INT NOT NULL,
                UnitPrice DECIMAL(18,2) NOT NULL,
                CONSTRAINT FK_OrderItems_Orders FOREIGN KEY (OrderId) REFERENCES dbo.Orders(Id) ON DELETE CASCADE,
                CONSTRAINT FK_OrderItems_Products FOREIGN KEY (ProductId) REFERENCES dbo.Products(Id)
            );");

        // 4. Склад товару (окремим пакетом, щоб працювало і для вже наявної БД)
        await connection.ExecuteAsync(@"
            IF COL_LENGTH(N'dbo.Products', N'Stock') IS NULL
                ALTER TABLE dbo.Products ADD Stock INT NOT NULL
                    CONSTRAINT DF_Products_Stock DEFAULT 10
                    CONSTRAINT CK_Products_Stock CHECK (Stock >= 0);");

        // 5. Seed без дублікатів
        await connection.ExecuteAsync(@"
            IF NOT EXISTS (SELECT 1 FROM dbo.Customers WHERE Id = @CustomerId)
                INSERT INTO dbo.Customers (Id, Name, Email) VALUES (@CustomerId, @CustomerName, @CustomerEmail);

            IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE Id = @ProductId)
                INSERT INTO dbo.Products (Id, Name, Price, Stock) VALUES (@ProductId, @ProductName, @Price, @Stock);",
            new
            {
                CustomerId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                CustomerName = "Іван Іваненко",
                CustomerEmail = "ivan@example.com",
                ProductId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                ProductName = "Gaming Laptop Pro",
                Price = 42000.00m,
                Stock = 10
            });
    }
}