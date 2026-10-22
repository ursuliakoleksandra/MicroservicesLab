/* =====================================================================
   Запити перевірки OrdersDb: CRUD, пошук, пагінація, транзакційні сценарії.
   Запускати після seed.sql (можна по блоках у SSMS).
   ===================================================================== */
SET QUOTED_IDENTIFIER ON;
GO
USE OrdersDb;
GO

/* 1. Самоперевірка: FOREIGN KEY лише всередині цієї БД (жодних посилань на Каталог) */
SELECT fk.name AS ForeignKey,
       OBJECT_NAME(fk.parent_object_id)     AS ChildTable,
       OBJECT_NAME(fk.referenced_object_id) AS ParentTable
FROM sys.foreign_keys fk ORDER BY ChildTable;

/* 2. Індекси (кожен описано в schema.sql) */
SELECT OBJECT_NAME(object_id) AS TableName, name AS IndexName, filter_definition
FROM sys.indexes WHERE object_id IN (SELECT object_id FROM sys.tables) AND name LIKE N'IX[_]%' ORDER BY TableName;

/* 3. CRUD по клієнтах і довіднику */
INSERT INTO dbo.Customers (Email, FullName, CreatedBy) VALUES (N'test.user@example.com', N'Тест Користувач', N'checks');   -- Create
SELECT Id, Email, FullName FROM dbo.Customers WHERE IsDeleted = 0 ORDER BY Id;                                              -- Read
UPDATE dbo.Customers SET Phone = N'+380509999999', UpdatedAt = SYSUTCDATETIME(), UpdatedBy = N'checks'
WHERE Email = N'test.user@example.com';                                                                                      -- Update
UPDATE dbo.Customers SET IsDeleted = 1 WHERE Email = N'test.user@example.com';                                               -- Delete (soft)
GO

/* 4. Читання, пошук і пагінація через процедури */
EXEC dbo.usp_GetOrderById @OrderId = 1;                                   -- 3 набори: шапка, позиції, історія
EXEC dbo.usp_GetCustomerOrders @CustomerId = 1;                           -- усі замовлення клієнта
EXEC dbo.usp_GetCustomerOrders @CustomerId = 1, @Status = N'Paid', @PageNumber = 1, @PageSize = 5;
SELECT * FROM dbo.vw_OrderSummary WHERE IsDeleted = 0 ORDER BY OrderId;   -- денормалізоване представлення
GO

/* 5. ТРАНЗАКЦІЙНА ЗМІНА СТАТУСУ: недопустимий перехід -> керована помилка, дія НЕ виконується */
DECLARE @newOrder BIGINT = (SELECT TOP (1) Id FROM dbo.Orders WHERE Status = N'Submitted' AND IsDeleted = 0 ORDER BY Id);
BEGIN TRY
    EXEC dbo.usp_ChangeOrderStatus @OrderId = @newOrder, @NewStatus = N'Shipped', @ChangedBy = N'checks';  -- відвантажити неоплачене
    PRINT N'ПОМИЛКА ПЕРЕВІРКИ: перехід мав бути відхилений!';
END TRY
BEGIN CATCH
    SELECT ERROR_NUMBER() AS ErrorNumber, ERROR_MESSAGE() AS ErrorMessage;   -- очікуємо 50002
END CATCH;
SELECT Id, Status FROM dbo.Orders WHERE Id = @newOrder;                       -- статус не змінився
GO

/* 6. Допустимий перехід і скасування оплаченого (має бути відхилено) */
DECLARE @o BIGINT = (SELECT TOP (1) Id FROM dbo.Orders WHERE Status = N'Submitted' AND IsDeleted = 0 ORDER BY Id);
EXEC dbo.usp_ChangeOrderStatus @OrderId = @o, @NewStatus = N'AwaitingValidation', @ChangedBy = N'checks';
BEGIN TRY
    EXEC dbo.usp_ChangeOrderStatus @OrderId = 1, @NewStatus = N'Cancelled', @ChangedBy = N'checks';   -- замовлення 1 вже Paid
END TRY
BEGIN CATCH
    SELECT ERROR_NUMBER() AS ErrorNumber, ERROR_MESSAGE() AS ErrorMessage;   -- очікуємо 50002
END CATCH;
GO

/* 7. Оптимістична конкурентність: застарілий RowVersion відхиляється (50008) */
DECLARE @oid BIGINT = (SELECT TOP (1) Id FROM dbo.Orders WHERE Status IN (N'Submitted', N'AwaitingValidation') AND IsDeleted = 0 ORDER BY Id);
DECLARE @rv BINARY(8) = (SELECT RowVersion FROM dbo.Orders WHERE Id = @oid);
EXEC dbo.usp_UpdateOrderDetails @OrderId = @oid, @ShippingAddress = N'Нова адреса, 1', @Comment = N'1-й користувач',
     @UpdatedBy = N'checks', @RowVersion = @rv;                                -- успіх
BEGIN TRY
    EXEC dbo.usp_UpdateOrderDetails @OrderId = @oid, @ShippingAddress = N'Інша адреса, 2', @Comment = N'2-й користувач',
         @UpdatedBy = N'checks', @RowVersion = @rv;                            -- той самий (вже застарілий) RowVersion
END TRY
BEGIN CATCH
    SELECT ERROR_NUMBER() AS ErrorNumber, ERROR_MESSAGE() AS ErrorMessage;   -- очікуємо 50008
END CATCH;
GO

/* 8. Знімок: зміна ціни в довіднику НЕ змінює вже створені позиції */
UPDATE dbo.Products SET Price = Price + 1000, SyncedAt = SYSUTCDATETIME() WHERE Id = 1;   -- імітація події з Каталогу
SELECT p.Price AS CurrentPrice, i.UnitPrice AS PriceInOrder
FROM dbo.Products p JOIN dbo.OrderItems i ON i.ProductId = p.Id WHERE p.Id = 1;
UPDATE dbo.Products SET Price = Price - 1000 WHERE Id = 1;                                  -- повертаємо як було
GO
