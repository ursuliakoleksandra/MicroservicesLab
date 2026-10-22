/* =====================================================================
   Проєкт №1 — збережувані процедури OrdersDb (6 шт.)
   Запуск: sqlcmd -S localhost -U sa -P <пароль> -C -I -i db/p1/procedures.sql
   Скрипт ідемпотентний (CREATE OR ALTER).

   Керовані помилки (THROW):
     50001 Order not found                50006 Product not found or inactive
     50002 Invalid status transition      50007 Items list is empty / invalid units
     50003 Unknown status value           50008 Concurrency conflict (RowVersion)
     50004 Order cannot be deleted        50009 Order can no longer be edited
     50005 Payment method required        50010 Customer not found
   ===================================================================== */
USE OrdersDb;
GO

/* 1. CREATE — створення замовлення з позиціями (знімок назви й ціни з локального довідника).
      @ItemsJson: [{"productId":1,"units":2}, ...] */
CREATE OR ALTER PROCEDURE dbo.usp_CreateOrder
    @CustomerId      BIGINT,
    @ShippingAddress NVARCHAR(400),
    @ItemsJson       NVARCHAR(MAX),
    @Comment         NVARCHAR(1000) = NULL,
    @CreatedBy       NVARCHAR(100)  = NULL,
    @OrderId         BIGINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Customers WHERE Id = @CustomerId AND IsDeleted = 0)
        THROW 50010, 'Customer not found', 1;

    DECLARE @Items TABLE (ProductId BIGINT NULL, Units INT NULL);
    INSERT INTO @Items (ProductId, Units)
    SELECT ProductId, SUM(Units)
    FROM OPENJSON(@ItemsJson) WITH (ProductId BIGINT '$.productId', Units INT '$.units')
    GROUP BY ProductId;

    IF NOT EXISTS (SELECT 1 FROM @Items) OR EXISTS (SELECT 1 FROM @Items WHERE ProductId IS NULL OR Units IS NULL OR Units <= 0)
        THROW 50007, 'Items list is empty or contains invalid units', 1;

    IF EXISTS (SELECT 1 FROM @Items i LEFT JOIN dbo.Products p ON p.Id = i.ProductId AND p.IsActive = 1 WHERE p.Id IS NULL)
        THROW 50006, 'Product not found or inactive', 1;

    BEGIN TRANSACTION;

    INSERT INTO dbo.Orders (CustomerId, CreatedBy) VALUES (@CustomerId, @CreatedBy);
    SET @OrderId = SCOPE_IDENTITY();

    INSERT INTO dbo.OrderDetails (OrderId, ShippingAddress, Comment)
    VALUES (@OrderId, @ShippingAddress, @Comment);

    INSERT INTO dbo.OrderItems (OrderId, ProductId, ProductName, UnitPrice, Units)
    SELECT @OrderId, p.Id, p.Name, p.Price, i.Units      -- ЗНІМОК назви й ціни
    FROM @Items i JOIN dbo.Products p ON p.Id = i.ProductId;

    INSERT INTO dbo.OrderStatusHistory (OrderId, OldStatus, NewStatus, ChangedBy)
    VALUES (@OrderId, NULL, N'Submitted', @CreatedBy);

    COMMIT TRANSACTION;
END
GO

/* 2. READ — замовлення за Id: 3 результуючі набори (шапка, позиції, історія).
      У шапці повертається RowVersion для оптимістичної конкурентності. */
CREATE OR ALTER PROCEDURE dbo.usp_GetOrderById
    @OrderId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Orders WHERE Id = @OrderId AND IsDeleted = 0)
        THROW 50001, 'Order not found', 1;

    SELECT s.OrderId, s.CustomerId, s.CustomerName, s.Status, s.OrderDate, s.Total,
           d.ShippingAddress, d.Comment, o.RowVersion
    FROM dbo.vw_OrderSummary s
    JOIN dbo.Orders o       ON o.Id = s.OrderId
    JOIN dbo.OrderDetails d ON d.OrderId = s.OrderId
    WHERE s.OrderId = @OrderId;

    SELECT ProductId, ProductName, UnitPrice, Discount, Units,
           UnitPrice * Units - Discount AS LineTotal
    FROM dbo.OrderItems WHERE OrderId = @OrderId ORDER BY ProductId;

    SELECT OldStatus, NewStatus, ChangedAt, ChangedBy
    FROM dbo.OrderStatusHistory WHERE OrderId = @OrderId ORDER BY ChangedAt, Id;
END
GO

/* 3. READ — пошук і пагінація: замовлення клієнта (опційно за статусом). */
CREATE OR ALTER PROCEDURE dbo.usp_GetCustomerOrders
    @CustomerId BIGINT,
    @Status     NVARCHAR(30) = NULL,
    @PageNumber INT = 1,
    @PageSize   INT = 20
AS
BEGIN
    SET NOCOUNT ON;
    IF @PageNumber < 1 SET @PageNumber = 1;
    IF @PageSize < 1 OR @PageSize > 100 SET @PageSize = 20;

    SELECT OrderId, Status, OrderDate, ItemsCount, Total,
           COUNT(*) OVER () AS TotalRows
    FROM dbo.vw_OrderSummary
    WHERE CustomerId = @CustomerId
      AND IsDeleted = 0
      AND (@Status IS NULL OR Status = @Status)
    ORDER BY OrderDate DESC, OrderId DESC
    OFFSET (@PageNumber - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

/* 4. UPDATE — зміна адреси/коментаря з оптимістичною конкурентністю.
      @RowVersion — значення, яке клієнт прочитав раніше (usp_GetOrderById). */
CREATE OR ALTER PROCEDURE dbo.usp_UpdateOrderDetails
    @OrderId         BIGINT,
    @ShippingAddress NVARCHAR(400),
    @Comment         NVARCHAR(1000) = NULL,
    @UpdatedBy       NVARCHAR(100)  = NULL,
    @RowVersion      BINARY(8)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    DECLARE @Status NVARCHAR(30), @Current BINARY(8);
    SELECT @Status = Status, @Current = RowVersion
    FROM dbo.Orders WITH (UPDLOCK, ROWLOCK)
    WHERE Id = @OrderId AND IsDeleted = 0;

    IF @Status IS NULL
        THROW 50001, 'Order not found', 1;
    IF @Status IN (N'Paid', N'Shipped', N'Cancelled')
        THROW 50009, 'Order can no longer be edited', 1;
    IF @Current <> @RowVersion
        THROW 50008, 'Concurrency conflict: the order was modified by another user', 1;

    UPDATE dbo.OrderDetails
    SET ShippingAddress = @ShippingAddress, Comment = @Comment
    WHERE OrderId = @OrderId;

    UPDATE dbo.Orders                                   -- змінює RowVersion
    SET UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @UpdatedBy
    WHERE Id = @OrderId;

    COMMIT TRANSACTION;
END
GO

/* 5. DELETE — м'яке видалення (дозволено лише для Submitted та Cancelled). */
CREATE OR ALTER PROCEDURE dbo.usp_SoftDeleteOrder
    @OrderId   BIGINT,
    @DeletedBy NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    DECLARE @Status NVARCHAR(30) =
        (SELECT Status FROM dbo.Orders WITH (UPDLOCK, ROWLOCK) WHERE Id = @OrderId AND IsDeleted = 0);

    IF @Status IS NULL
        THROW 50001, 'Order not found', 1;
    IF @Status NOT IN (N'Submitted', N'Cancelled')
        THROW 50004, 'Only Submitted or Cancelled orders can be deleted', 1;

    UPDATE dbo.Orders
    SET IsDeleted = 1, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @DeletedBy
    WHERE Id = @OrderId;

    COMMIT TRANSACTION;
END
GO

/* 6. БІЗНЕС-ОПЕРАЦІЯ — транзакційна зміна статусу з перевіркою переходу.
      Допустимі переходи:
        Submitted -> AwaitingValidation -> StockConfirmed -> Paid -> Shipped
        Cancelled — лише з Submitted / AwaitingValidation / StockConfirmed (до оплати).
      Перехід у Paid створює PaymentRecord (потрібен @PaymentMethod).
      Недопустимий перехід -> керована помилка 50002, зміни відкочуються. */
CREATE OR ALTER PROCEDURE dbo.usp_ChangeOrderStatus
    @OrderId       BIGINT,
    @NewStatus     NVARCHAR(30),
    @ChangedBy     NVARCHAR(100) = NULL,
    @PaymentMethod NVARCHAR(30)  = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;                                 -- будь-яка помилка відкочує транзакцію

    IF @NewStatus NOT IN (N'Submitted', N'AwaitingValidation', N'StockConfirmed', N'Paid', N'Shipped', N'Cancelled')
        THROW 50003, 'Unknown status value', 1;
    IF @NewStatus = N'Paid' AND (@PaymentMethod IS NULL OR @PaymentMethod NOT IN (N'Card', N'Cash', N'Transfer'))
        THROW 50005, 'Payment method (Card, Cash or Transfer) is required for Paid status', 1;

    BEGIN TRANSACTION;

    -- UPDLOCK блокує рядок до кінця транзакції: два паралельні переходи не «перетнуться»
    DECLARE @Old NVARCHAR(30) =
        (SELECT Status FROM dbo.Orders WITH (UPDLOCK, ROWLOCK) WHERE Id = @OrderId AND IsDeleted = 0);

    IF @Old IS NULL
        THROW 50001, 'Order not found', 1;

    IF NOT EXISTS (
        SELECT 1
        FROM (VALUES (N'Submitted',          N'AwaitingValidation'),
                     (N'AwaitingValidation', N'StockConfirmed'),
                     (N'StockConfirmed',     N'Paid'),
                     (N'Paid',               N'Shipped'),
                     (N'Submitted',          N'Cancelled'),
                     (N'AwaitingValidation', N'Cancelled'),
                     (N'StockConfirmed',     N'Cancelled')) AS t (FromStatus, ToStatus)
        WHERE t.FromStatus = @Old AND t.ToStatus = @NewStatus)
    BEGIN
        DECLARE @Msg NVARCHAR(200) = CONCAT(N'Invalid status transition: ', @Old, N' -> ', @NewStatus);
        THROW 50002, @Msg, 1;
    END

    IF @NewStatus = N'Paid'
    BEGIN
        DECLARE @Amount DECIMAL(18,2) =
            (SELECT SUM(UnitPrice * Units - Discount) FROM dbo.OrderItems WHERE OrderId = @OrderId);
        INSERT INTO dbo.PaymentRecords (OrderId, Amount, Method) VALUES (@OrderId, @Amount, @PaymentMethod);
    END

    UPDATE dbo.Orders
    SET Status = @NewStatus, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = @ChangedBy
    WHERE Id = @OrderId;

    INSERT INTO dbo.OrderStatusHistory (OrderId, OldStatus, NewStatus, ChangedBy)
    VALUES (@OrderId, @Old, @NewStatus, @ChangedBy);

    COMMIT TRANSACTION;
END
GO
