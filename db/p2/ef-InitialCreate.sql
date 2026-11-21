/* =====================================================================
   SQL-скрипт міграції InitialCreate (CatalogDb, EF Core 9) — еквівалент виводу
   dotnet ef migrations script --project Services/Catalog/Catalog.Dal --startup-project Services/Catalog/Catalog.Api
   Призначений для перегляду або застосування змін вручну (наприклад, у production, де MigrateAsync вимкнено).
   Якщо ви перегенерували міграцію командою dotnet ef — замініть цей файл її виводом.
   ===================================================================== */
IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (SELECT * FROM [__EFMigrationsHistory] WHERE [MigrationId] = N'20261018120000_InitialCreate')
BEGIN
    CREATE TABLE [Brands] (
        [Id] bigint NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [Country] nvarchar(100) NULL,
        CONSTRAINT [PK_Brands] PRIMARY KEY ([Id])
    );

    CREATE TABLE [Categories] (
        [Id] bigint NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [Slug] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_Categories] PRIMARY KEY ([Id])
    );

    CREATE TABLE [Products] (
        [Id] bigint NOT NULL IDENTITY,
        [BrandId] bigint NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Sku] nvarchar(50) NOT NULL,
        [Price] decimal(18,2) NOT NULL,
        [AvailableStock] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] datetime2 NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Products] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Products_AvailableStock] CHECK ([AvailableStock] >= 0),
        CONSTRAINT [CK_Products_Price] CHECK ([Price] > 0),
        CONSTRAINT [FK_Products_Brands_BrandId] FOREIGN KEY ([BrandId]) REFERENCES [Brands] ([Id]) ON DELETE NO ACTION
    );

    CREATE TABLE [ProductCategories] (
        [ProductId] bigint NOT NULL,
        [CategoryId] bigint NOT NULL,
        [AddedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_ProductCategories] PRIMARY KEY ([ProductId], [CategoryId]),
        CONSTRAINT [FK_ProductCategories_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ProductCategories_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
    );

    CREATE TABLE [ProductDetails] (
        [ProductId] bigint NOT NULL,
        [Description] nvarchar(2000) NULL,
        [WeightGrams] int NULL,
        [WarrantyMonths] int NOT NULL,
        [Specs] nvarchar(max) NULL,
        CONSTRAINT [PK_ProductDetails] PRIMARY KEY ([ProductId]),
        CONSTRAINT [CK_ProductDetails_Specs] CHECK ([Specs] IS NULL OR ISJSON([Specs]) = 1),
        CONSTRAINT [CK_ProductDetails_WarrantyMonths] CHECK ([WarrantyMonths] >= 0),
        CONSTRAINT [CK_ProductDetails_WeightGrams] CHECK ([WeightGrams] > 0),
        CONSTRAINT [FK_ProductDetails_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
    );

    CREATE TABLE [ProductImages] (
        [Id] bigint NOT NULL IDENTITY,
        [ProductId] bigint NOT NULL,
        [Url] nvarchar(500) NOT NULL,
        [SortOrder] int NOT NULL,
        [IsMain] bit NOT NULL,
        CONSTRAINT [PK_ProductImages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProductImages_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([Id]) ON DELETE CASCADE
    );

    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Country', N'Name') AND [object_id] = OBJECT_ID(N'[Brands]'))
        SET IDENTITY_INSERT [Brands] ON;
    INSERT INTO [Brands] ([Id], [Country], [Name])
    VALUES (CAST(1 AS bigint), N'USA', N'Nova'),
           (CAST(2 AS bigint), N'South Korea', N'Aurix'),
           (CAST(3 AS bigint), N'Japan', N'Sonic'),
           (CAST(4 AS bigint), N'Germany', N'Gridline'),
           (CAST(5 AS bigint), N'Taiwan', N'Lumen');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Country', N'Name') AND [object_id] = OBJECT_ID(N'[Brands]'))
        SET IDENTITY_INSERT [Brands] OFF;

    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name', N'Slug') AND [object_id] = OBJECT_ID(N'[Categories]'))
        SET IDENTITY_INSERT [Categories] ON;
    INSERT INTO [Categories] ([Id], [Name], [Slug])
    VALUES (CAST(1 AS bigint), N'Ноутбуки', N'laptops'),
           (CAST(2 AS bigint), N'Смартфони', N'smartphones'),
           (CAST(3 AS bigint), N'Аудіо', N'audio'),
           (CAST(4 AS bigint), N'Аксесуари', N'accessories'),
           (CAST(5 AS bigint), N'Монітори', N'monitors'),
           (CAST(6 AS bigint), N'Для геймерів', N'gaming');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Name', N'Slug') AND [object_id] = OBJECT_ID(N'[Categories]'))
        SET IDENTITY_INSERT [Categories] OFF;

    CREATE UNIQUE INDEX [IX_Brands_Name] ON [Brands] ([Name]);
    CREATE UNIQUE INDEX [IX_Categories_Name] ON [Categories] ([Name]);
    CREATE UNIQUE INDEX [IX_Categories_Slug] ON [Categories] ([Slug]);
    CREATE INDEX [IX_ProductCategories_CategoryId] ON [ProductCategories] ([CategoryId]);
    CREATE INDEX [IX_ProductImages_ProductId_SortOrder] ON [ProductImages] ([ProductId], [SortOrder]);
    CREATE UNIQUE INDEX [UX_ProductImages_Main] ON [ProductImages] ([ProductId]) WHERE [IsMain] = 1;
    CREATE INDEX [IX_Products_BrandId] ON [Products] ([BrandId]);
    CREATE INDEX [IX_Products_Name] ON [Products] ([Name]);
    CREATE UNIQUE INDEX [IX_Products_Sku] ON [Products] ([Sku]);

    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261018120000_InitialCreate', N'9.0.0');
END;
GO

COMMIT;
GO
