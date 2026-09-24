using Dapper;
using Microsoft.Data.SqlClient;

namespace Orders.Dal;

public static class DbInitializer
{
    public static async Task InitializeAsync(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        var databaseName = builder.InitialCatalog;

        // 1. Створення самісінької БД, якщо її не існує
        builder.InitialCatalog = "master";
        await using (var masterConnection = new SqlConnection(builder.ConnectionString))
        {
            await masterConnection.OpenAsync();
            var createDbSql = $@"
                IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = '{databaseName}')
                BEGIN
                    CREATE DATABASE [{databaseName}];
                END;";
            await masterConnection.ExecuteAsync(createDbSql);
        }

        // 2. Підключаємося до нашої БД OrdersDb
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        // 3. Примусово видаляємо всі старі зовнішні ключі та таблиці з помилковою структурою
      /*  var dropAllSql = @"
            DECLARE @sql NVARCHAR(MAX) = N'';
            SELECT @sql += N'ALTER TABLE ' + QUOTENAME(OBJECT_SCHEMA_NAME(parent_object_id))
                + '.' + QUOTENAME(OBJECT_NAME(parent_object_id)) 
                + ' DROP CONSTRAINT ' + QUOTENAME(name) + ';' + CHAR(13)
            FROM sys.foreign_keys;
            EXEC sp_executesql @sql;

            IF EXISTS (SELECT * FROM sys.tables WHERE name = 'OrderItems') DROP TABLE OrderItems;
            IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Orders') DROP TABLE Orders;
            IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Customers') DROP TABLE Customers;
            IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Products') DROP TABLE Products;
        ";
        await connection.ExecuteAsync(dropAllSql); */

        // 4. Створення нових, 100% правильних таблиць
        var createTablesSql = @"
            CREATE TABLE Customers (
                Id UNIQUEIDENTIFIER PRIMARY KEY,
                Name NVARCHAR(100) NOT NULL,
                Email NVARCHAR(100) NOT NULL
            );

            CREATE TABLE Products (
                Id UNIQUEIDENTIFIER PRIMARY KEY,
                Name NVARCHAR(100) NOT NULL,
                Price DECIMAL(18,2) NOT NULL
            );

            CREATE TABLE Orders (
                Id UNIQUEIDENTIFIER PRIMARY KEY,
                CustomerId UNIQUEIDENTIFIER NOT NULL,
                OrderDate DATETIME2 NOT NULL,
                TotalAmount DECIMAL(18,2) NOT NULL,
                Status INT NOT NULL,
                CONSTRAINT FK_Orders_Customers FOREIGN KEY (CustomerId) REFERENCES Customers(Id)
            );

            CREATE TABLE OrderItems (
                Id UNIQUEIDENTIFIER PRIMARY KEY,
                OrderId UNIQUEIDENTIFIER NOT NULL,
                ProductId UNIQUEIDENTIFIER NOT NULL,
                Quantity INT NOT NULL,
                UnitPrice DECIMAL(18,2) NOT NULL,
                CONSTRAINT FK_OrderItems_Orders FOREIGN KEY (OrderId) REFERENCES Orders(Id) ON DELETE CASCADE,
                CONSTRAINT FK_OrderItems_Products FOREIGN KEY (ProductId) REFERENCES Products(Id)
            );
        ";
        await connection.ExecuteAsync(createTablesSql);

        // 5. Наповнення тестовими даними (Seed Data)
        var customerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var productId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        await connection.ExecuteAsync(
            "INSERT INTO Customers (Id, Name, Email) VALUES (@Id, @Name, @Email)",
            new { Id = customerId, Name = "Іван Іваненко", Email = "ivan@example.com" });

        await connection.ExecuteAsync(
            "INSERT INTO Products (Id, Name, Price) VALUES (@Id, @Name, @Price)",
            new { Id = productId, Name = "Gaming Laptop Pro", Price = 42000.00m });
    }
}