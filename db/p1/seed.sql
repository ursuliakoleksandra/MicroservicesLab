/* =====================================================================
   Проєкт №1 — тестові дані OrdersDb. ІДЕМПОТЕНТНИЙ: перед кожною вставкою
   перевіряється наявність запису. Запускати ПІСЛЯ schema.sql і procedures.sql.
   sqlcmd -S localhost -U sa -P <пароль> -C -I -i db/p1/seed.sql
   ===================================================================== */
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO
USE OrdersDb;
GO

/* Клієнти */
IF NOT EXISTS (SELECT 1 FROM dbo.Customers WHERE Email = N'olena.melnyk@example.com')
    INSERT INTO dbo.Customers (Email, FullName, Phone, CreatedBy) VALUES (N'olena.melnyk@example.com', N'Олена Мельник', N'+380501110001', N'seed');
IF NOT EXISTS (SELECT 1 FROM dbo.Customers WHERE Email = N'andrii.koval@example.com')
    INSERT INTO dbo.Customers (Email, FullName, Phone, CreatedBy) VALUES (N'andrii.koval@example.com', N'Андрій Коваль', N'+380501110002', N'seed');
IF NOT EXISTS (SELECT 1 FROM dbo.Customers WHERE Email = N'iryna.shevchenko@example.com')
    INSERT INTO dbo.Customers (Email, FullName, Phone, CreatedBy) VALUES (N'iryna.shevchenko@example.com', N'Ірина Шевченко', NULL, N'seed');
GO

/* Локальна копія довідника Каталогу (ті самі Id, що в CatalogDb/db/p2/seed.sql) */
INSERT INTO dbo.Products (Id, Name, Price)
SELECT v.Id, v.Name, v.Price
FROM (VALUES (1, N'Ноутбук X15',            32999.00),
             (2, N'Смартфон S10',           18999.00),
             (3, N'Навушники Air',           3499.00),
             (4, N'Миша ігрова M7',          1299.00),
             (5, N'Монітор 27" QHD',        11999.00)) AS v (Id, Name, Price)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Products p WHERE p.Id = v.Id);
GO

/* Замовлення створюються лише якщо їх ще немає (повторний запуск нічого не додає) */
IF NOT EXISTS (SELECT 1 FROM dbo.Orders)
BEGIN
    DECLARE @c1 BIGINT = (SELECT Id FROM dbo.Customers WHERE Email = N'olena.melnyk@example.com');
    DECLARE @c2 BIGINT = (SELECT Id FROM dbo.Customers WHERE Email = N'andrii.koval@example.com');
    DECLARE @c3 BIGINT = (SELECT Id FROM dbo.Customers WHERE Email = N'iryna.shevchenko@example.com');
    DECLARE @o1 BIGINT, @o2 BIGINT, @o3 BIGINT;

    -- Замовлення 1: Олена, повний цикл до статусу Paid
    EXEC dbo.usp_CreateOrder @CustomerId = @c1, @ShippingAddress = N'Чернівці, вул. Руська, 10, кв. 5',
         @ItemsJson = N'[{"productId":1,"units":1},{"productId":4,"units":2}]',
         @Comment = N'Подзвонити перед доставкою', @CreatedBy = N'seed', @OrderId = @o1 OUTPUT;
    EXEC dbo.usp_ChangeOrderStatus @OrderId = @o1, @NewStatus = N'AwaitingValidation', @ChangedBy = N'seed';
    EXEC dbo.usp_ChangeOrderStatus @OrderId = @o1, @NewStatus = N'StockConfirmed',     @ChangedBy = N'seed';
    EXEC dbo.usp_ChangeOrderStatus @OrderId = @o1, @NewStatus = N'Paid',               @ChangedBy = N'seed', @PaymentMethod = N'Card';

    -- Замовлення 2: Андрій, нове (Submitted) — на ньому демонструємо недопустимий перехід
    EXEC dbo.usp_CreateOrder @CustomerId = @c2, @ShippingAddress = N'Київ, просп. Науки, 1',
         @ItemsJson = N'[{"productId":2,"units":1},{"productId":3,"units":1}]',
         @CreatedBy = N'seed', @OrderId = @o2 OUTPUT;

    -- Замовлення 3: Ірина, скасоване
    EXEC dbo.usp_CreateOrder @CustomerId = @c3, @ShippingAddress = N'Львів, вул. Городоцька, 77',
         @ItemsJson = N'[{"productId":5,"units":1}]', @CreatedBy = N'seed', @OrderId = @o3 OUTPUT;
    EXEC dbo.usp_ChangeOrderStatus @OrderId = @o3, @NewStatus = N'Cancelled', @ChangedBy = N'seed';
END
GO
