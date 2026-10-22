/* =====================================================================
   Проєкт №2 — CatalogDb (SQL Server). Контекст «Каталог».
   Запуск: sqlcmd -S localhost -U sa -P <пароль> -C -I -i db/p2/schema.sql
   Ідемпотентний. У Лаб. 03 ця схема реалізується в EF Core (Code First, Fluent API);
   схема сумісна з міграціями (зміни структури — окремими скриптами/міграціями).

   PK: BIGINT IDENTITY — вставки з одного джерела; Id товару далі використовують
   інші сервіси (OrdersDb.Products.Id, ReviewsDb.productId) — без FK між базами.
   ===================================================================== */
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
IF DB_ID(N'CatalogDb') IS NULL CREATE DATABASE CatalogDb;
GO
USE CatalogDb;
GO

IF OBJECT_ID(N'dbo.Brands', N'U') IS NULL
CREATE TABLE dbo.Brands (
    Id      BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Brands PRIMARY KEY,
    Name    NVARCHAR(100) NOT NULL CONSTRAINT UQ_Brands_Name UNIQUE,
    Country NVARCHAR(100) NULL
);
GO

IF OBJECT_ID(N'dbo.Categories', N'U') IS NULL
CREATE TABLE dbo.Categories (
    Id   BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Categories PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL CONSTRAINT UQ_Categories_Name UNIQUE,
    Slug NVARCHAR(100) NOT NULL CONSTRAINT UQ_Categories_Slug UNIQUE
);
GO

/* 1:N  Brands -> Products (бренд з товарами не каскадимо) */
IF OBJECT_ID(N'dbo.Products', N'U') IS NULL
CREATE TABLE dbo.Products (
    Id        BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Products PRIMARY KEY,
    BrandId   BIGINT NOT NULL CONSTRAINT FK_Products_Brands FOREIGN KEY REFERENCES dbo.Brands(Id),
    Sku       NVARCHAR(50)  NOT NULL CONSTRAINT UQ_Products_Sku UNIQUE,
    Name      NVARCHAR(200) NOT NULL,
    Price     DECIMAL(18,2) NOT NULL CONSTRAINT CK_Products_Price CHECK (Price > 0),
    IsActive  BIT NOT NULL CONSTRAINT DF_Products_IsActive DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Products_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt DATETIME2 NULL
);
GO

/* 1:1  Products <-> ProductDetails (FK = PK) */
IF OBJECT_ID(N'dbo.ProductDetails', N'U') IS NULL
CREATE TABLE dbo.ProductDetails (
    ProductId      BIGINT NOT NULL CONSTRAINT PK_ProductDetails PRIMARY KEY
                   CONSTRAINT FK_ProductDetails_Products FOREIGN KEY REFERENCES dbo.Products(Id) ON DELETE CASCADE,
    Description    NVARCHAR(2000) NULL,
    WeightGrams    INT NULL CONSTRAINT CK_ProductDetails_Weight CHECK (WeightGrams > 0),
    WarrantyMonths INT NOT NULL CONSTRAINT DF_ProductDetails_Warranty DEFAULT 12
                   CONSTRAINT CK_ProductDetails_Warranty CHECK (WarrantyMonths >= 0),
    Specs          NVARCHAR(MAX) NULL CONSTRAINT CK_ProductDetails_Specs CHECK (Specs IS NULL OR ISJSON(Specs) = 1)
);
GO

/* M:N  Products <-> Categories з власним полем AddedAt */
IF OBJECT_ID(N'dbo.ProductCategories', N'U') IS NULL
CREATE TABLE dbo.ProductCategories (
    ProductId  BIGINT NOT NULL CONSTRAINT FK_ProductCategories_Products   FOREIGN KEY REFERENCES dbo.Products(Id) ON DELETE CASCADE,
    CategoryId BIGINT NOT NULL CONSTRAINT FK_ProductCategories_Categories FOREIGN KEY REFERENCES dbo.Categories(Id) ON DELETE CASCADE,
    AddedAt    DATETIME2 NOT NULL CONSTRAINT DF_ProductCategories_AddedAt DEFAULT SYSUTCDATETIME(),  -- власне поле M:N
    CONSTRAINT PK_ProductCategories PRIMARY KEY (ProductId, CategoryId)
);
GO

/* 1:N  Products -> ProductImages */
IF OBJECT_ID(N'dbo.ProductImages', N'U') IS NULL
CREATE TABLE dbo.ProductImages (
    Id        BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ProductImages PRIMARY KEY,
    ProductId BIGINT NOT NULL CONSTRAINT FK_ProductImages_Products FOREIGN KEY REFERENCES dbo.Products(Id) ON DELETE CASCADE,
    Url       NVARCHAR(500) NOT NULL,
    SortOrder INT NOT NULL CONSTRAINT DF_ProductImages_SortOrder DEFAULT 0,
    IsMain    BIT NOT NULL CONSTRAINT DF_ProductImages_IsMain DEFAULT 0
);
GO

/* ---------- ІНДЕКСИ (кожен — під запит) ---------- */
-- Запит: «товари бренду X»
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Products_BrandId' AND object_id = OBJECT_ID(N'dbo.Products'))
    CREATE INDEX IX_Products_BrandId ON dbo.Products (BrandId);
-- Запит: пошук і сортування за назвою, лише активні товари (покриває ціну для списку)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Products_Name_Active' AND object_id = OBJECT_ID(N'dbo.Products'))
    CREATE INDEX IX_Products_Name_Active ON dbo.Products (Name) INCLUDE (Price) WHERE IsActive = 1;
-- Запит: «товари категорії X» (зворотний бік M:N; PK покриває лише напрям Product -> Category)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProductCategories_CategoryId' AND object_id = OBJECT_ID(N'dbo.ProductCategories'))
    CREATE INDEX IX_ProductCategories_CategoryId ON dbo.ProductCategories (CategoryId) INCLUDE (AddedAt);
-- Запит: «зображення товару за порядком»
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProductImages_ProductId_SortOrder' AND object_id = OBJECT_ID(N'dbo.ProductImages'))
    CREATE INDEX IX_ProductImages_ProductId_SortOrder ON dbo.ProductImages (ProductId, SortOrder);
-- Правило: у товару не більше одного головного зображення (унікальний фільтрований індекс)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_ProductImages_Main' AND object_id = OBJECT_ID(N'dbo.ProductImages'))
    CREATE UNIQUE INDEX UX_ProductImages_Main ON dbo.ProductImages (ProductId) WHERE IsMain = 1;
GO
