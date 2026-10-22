/* =====================================================================
   Проєкт №1 — OrdersDb (SQL Server). Контекст «Замовлення».
   Запуск:  sqlcmd -S localhost -U sa -P <пароль> -C -I -i db/p1/schema.sql
   (-I вмикає QUOTED_IDENTIFIER, потрібний для фільтрованих індексів)
   Скрипт ідемпотентний: повторний запуск нічого не ламає.

   Вибір первинних ключів: BIGINT IDENTITY — вставки йдуть з одного джерела
   (одна БД), ключ компактний і послідовний (менше фрагментації кластерного
   індексу). Products.Id — НЕ IDENTITY: це Id із CatalogDb.
   Між базами FOREIGN KEY немає: Products.Id і OrderItems.ProductId
   посилаються на Каталог лише логічно.
   ===================================================================== */
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
IF DB_ID(N'OrdersDb') IS NULL CREATE DATABASE OrdersDb;
GO
USE OrdersDb;
GO

/* ---------- Customers (в eShop — Buyer) ---------- */
IF OBJECT_ID(N'dbo.Customers', N'U') IS NULL
CREATE TABLE dbo.Customers (
    Id        BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Customers PRIMARY KEY,
    Email     NVARCHAR(256) NOT NULL CONSTRAINT UQ_Customers_Email UNIQUE,
    FullName  NVARCHAR(200) NOT NULL,
    Phone     NVARCHAR(30)  NULL,
    CreatedAt DATETIME2     NOT NULL CONSTRAINT DF_Customers_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy NVARCHAR(100) NULL,
    UpdatedAt DATETIME2     NULL,
    UpdatedBy NVARCHAR(100) NULL,
    IsDeleted BIT           NOT NULL CONSTRAINT DF_Customers_IsDeleted DEFAULT 0,
    RowVersion ROWVERSION   NOT NULL,
    CONSTRAINT CK_Customers_Email CHECK (Email LIKE N'%_@_%._%')
);
GO

/* ---------- Orders: 1:N  Customers -> Orders ---------- */
IF OBJECT_ID(N'dbo.Orders', N'U') IS NULL
CREATE TABLE dbo.Orders (
    Id         BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Orders PRIMARY KEY,
    CustomerId BIGINT NOT NULL
               CONSTRAINT FK_Orders_Customers FOREIGN KEY REFERENCES dbo.Customers(Id), -- NO ACTION: клієнта з історією не каскадимо
    Status     NVARCHAR(30) NOT NULL CONSTRAINT DF_Orders_Status DEFAULT N'Submitted'
               CONSTRAINT CK_Orders_Status CHECK (Status IN
                   (N'Submitted', N'AwaitingValidation', N'StockConfirmed', N'Paid', N'Shipped', N'Cancelled')),
    OrderDate  DATETIME2 NOT NULL CONSTRAINT DF_Orders_OrderDate DEFAULT SYSUTCDATETIME(),
    -- аудит, soft delete, оптимістична конкурентність
    CreatedAt  DATETIME2     NOT NULL CONSTRAINT DF_Orders_CreatedAt DEFAULT SYSUTCDATETIME(),
    CreatedBy  NVARCHAR(100) NULL,
    UpdatedAt  DATETIME2     NULL,
    UpdatedBy  NVARCHAR(100) NULL,
    IsDeleted  BIT           NOT NULL CONSTRAINT DF_Orders_IsDeleted DEFAULT 0,
    RowVersion ROWVERSION    NOT NULL
);
GO

/* ---------- OrderDetails: 1:1  Orders <-> OrderDetails (FK = PK) ---------- */
IF OBJECT_ID(N'dbo.OrderDetails', N'U') IS NULL
CREATE TABLE dbo.OrderDetails (
    OrderId         BIGINT NOT NULL CONSTRAINT PK_OrderDetails PRIMARY KEY
                    CONSTRAINT FK_OrderDetails_Orders FOREIGN KEY REFERENCES dbo.Orders(Id) ON DELETE CASCADE,
    ShippingAddress NVARCHAR(400)  NOT NULL,
    Comment         NVARCHAR(1000) NULL
);
GO

/* ---------- Products: ЛОКАЛЬНА КОПІЯ довідника Каталогу ---------- */
IF OBJECT_ID(N'dbo.Products', N'U') IS NULL
CREATE TABLE dbo.Products (
    Id       BIGINT NOT NULL CONSTRAINT PK_Products PRIMARY KEY,   -- той самий Id, що в CatalogDb (не IDENTITY)
    Name     NVARCHAR(200) NOT NULL,                                -- копія, оновлюється подіями з Каталогу
    Price    DECIMAL(18,2) NOT NULL CONSTRAINT CK_Products_Price CHECK (Price > 0),
    IsActive BIT NOT NULL CONSTRAINT DF_Products_IsActive DEFAULT 1,
    SyncedAt DATETIME2 NOT NULL CONSTRAINT DF_Products_SyncedAt DEFAULT SYSUTCDATETIME()
);
GO

/* ---------- OrderItems: M:N  Orders <-> Products ---------- */
IF OBJECT_ID(N'dbo.OrderItems', N'U') IS NULL
CREATE TABLE dbo.OrderItems (
    OrderId     BIGINT NOT NULL
                CONSTRAINT FK_OrderItems_Orders FOREIGN KEY REFERENCES dbo.Orders(Id) ON DELETE CASCADE, -- позиції не живуть без замовлення
    ProductId   BIGINT NOT NULL
                CONSTRAINT FK_OrderItems_Products FOREIGN KEY REFERENCES dbo.Products(Id),
    ProductName NVARCHAR(200) NOT NULL,   -- ЗНІМОК на момент замовлення (власник: Product у CatalogDb)
    UnitPrice   DECIMAL(18,2) NOT NULL CONSTRAINT CK_OrderItems_UnitPrice CHECK (UnitPrice > 0), -- ЗНІМОК
    Discount    DECIMAL(18,2) NOT NULL CONSTRAINT DF_OrderItems_Discount DEFAULT 0
                CONSTRAINT CK_OrderItems_Discount CHECK (Discount >= 0),   -- сума знижки на весь рядок
    Units       INT NOT NULL CONSTRAINT CK_OrderItems_Units CHECK (Units > 0),
    CONSTRAINT PK_OrderItems PRIMARY KEY (OrderId, ProductId),
    CONSTRAINT CK_OrderItems_DiscountLimit CHECK (Discount <= UnitPrice * Units)
);
GO

/* ---------- OrderStatusHistory: 1:N Orders -> історія статусів ---------- */
IF OBJECT_ID(N'dbo.OrderStatusHistory', N'U') IS NULL
CREATE TABLE dbo.OrderStatusHistory (
    Id        BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_OrderStatusHistory PRIMARY KEY,
    OrderId   BIGINT NOT NULL
              CONSTRAINT FK_OrderStatusHistory_Orders FOREIGN KEY REFERENCES dbo.Orders(Id) ON DELETE CASCADE,
    OldStatus NVARCHAR(30) NULL,
    NewStatus NVARCHAR(30) NOT NULL,
    ChangedAt DATETIME2 NOT NULL CONSTRAINT DF_OrderStatusHistory_ChangedAt DEFAULT SYSUTCDATETIME(),
    ChangedBy NVARCHAR(100) NULL
);
GO

/* ---------- PaymentRecords: 1:N Orders -> оплати ---------- */
IF OBJECT_ID(N'dbo.PaymentRecords', N'U') IS NULL
CREATE TABLE dbo.PaymentRecords (
    Id      BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PaymentRecords PRIMARY KEY,
    OrderId BIGINT NOT NULL
            CONSTRAINT FK_PaymentRecords_Orders FOREIGN KEY REFERENCES dbo.Orders(Id), -- платежі не каскадимо
    Amount  DECIMAL(18,2) NOT NULL CONSTRAINT CK_PaymentRecords_Amount CHECK (Amount > 0),
    Method  NVARCHAR(30)  NOT NULL CONSTRAINT CK_PaymentRecords_Method CHECK (Method IN (N'Card', N'Cash', N'Transfer')),
    PaidAt  DATETIME2     NOT NULL CONSTRAINT DF_PaymentRecords_PaidAt DEFAULT SYSUTCDATETIME()
);
GO

/* =====================================================================
   ІНДЕКСИ — кожен створено під конкретний запит
   ===================================================================== */
-- Запит: «замовлення клієнта з певним статусом» (+ сортування за датою без key lookup)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Orders_CustomerId_Status' AND object_id = OBJECT_ID(N'dbo.Orders'))
    CREATE INDEX IX_Orders_CustomerId_Status ON dbo.Orders (CustomerId, Status) INCLUDE (OrderDate);

-- Запит: «усі живі замовлення клієнта» (фільтрований індекс: soft-deleted не потрапляють в індекс)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Orders_Active' AND object_id = OBJECT_ID(N'dbo.Orders'))
    CREATE INDEX IX_Orders_Active ON dbo.Orders (CustomerId) WHERE IsDeleted = 0;

-- Запит: «черга оператора — нові замовлення за статусом, від найстаріших» (фільтрований, покриває дату)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Orders_Status_OrderDate' AND object_id = OBJECT_ID(N'dbo.Orders'))
    CREATE INDEX IX_Orders_Status_OrderDate ON dbo.Orders (Status, OrderDate) WHERE IsDeleted = 0;

-- Запит: «у яких замовленнях є товар X» (зворотний пошук; PK (OrderId, ProductId) його не покриває)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OrderItems_ProductId' AND object_id = OBJECT_ID(N'dbo.OrderItems'))
    CREATE INDEX IX_OrderItems_ProductId ON dbo.OrderItems (ProductId) INCLUDE (Units);

-- Запит: «історія статусів замовлення у хронологічному порядку»
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OrderStatusHistory_OrderId_ChangedAt' AND object_id = OBJECT_ID(N'dbo.OrderStatusHistory'))
    CREATE INDEX IX_OrderStatusHistory_OrderId_ChangedAt ON dbo.OrderStatusHistory (OrderId, ChangedAt);

-- Запит: «платежі за замовленням»
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PaymentRecords_OrderId' AND object_id = OBJECT_ID(N'dbo.PaymentRecords'))
    CREATE INDEX IX_PaymentRecords_OrderId ON dbo.PaymentRecords (OrderId) INCLUDE (Amount);
-- Запит «клієнт за email» обслуговує UNIQUE-обмеження UQ_Customers_Email.
GO

/* ---------- Представлення для читання (денормалізоване) ---------- */
CREATE OR ALTER VIEW dbo.vw_OrderSummary
AS
SELECT  o.Id AS OrderId,
        o.CustomerId,
        c.FullName AS CustomerName,
        o.Status,
        o.OrderDate,
        o.IsDeleted,
        ISNULL(t.ItemsCount, 0) AS ItemsCount,
        ISNULL(t.Total, 0)      AS Total
FROM dbo.Orders o
JOIN dbo.Customers c ON c.Id = o.CustomerId
OUTER APPLY (SELECT SUM(i.Units) AS ItemsCount,
                    SUM(i.UnitPrice * i.Units - i.Discount) AS Total
             FROM dbo.OrderItems i WHERE i.OrderId = o.Id) t;
GO
