/* =====================================================================
   Проєкт №2 — довідникові дані CatalogDb. ІДЕМПОТЕНТНИЙ (NOT EXISTS).
   Id товарів 1..5 зафіксовано й збігаються з OrdersDb/db/p1/seed.sql та ReviewsDb.
   ===================================================================== */
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO
USE CatalogDb;
GO

INSERT INTO dbo.Brands (Name, Country)
SELECT v.Name, v.Country
FROM (VALUES (N'Nova', N'USA'), (N'Aurix', N'South Korea'), (N'Sonic', N'Japan'),
             (N'Gridline', N'Germany'), (N'Lumen', N'Taiwan')) AS v (Name, Country)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Brands b WHERE b.Name = v.Name);

INSERT INTO dbo.Categories (Name, Slug)
SELECT v.Name, v.Slug
FROM (VALUES (N'Ноутбуки', N'laptops'), (N'Смартфони', N'smartphones'), (N'Аудіо', N'audio'),
             (N'Аксесуари', N'accessories'), (N'Монітори', N'monitors'), (N'Для геймерів', N'gaming')) AS v (Name, Slug)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Categories c WHERE c.Slug = v.Slug);

SET IDENTITY_INSERT dbo.Products ON;
INSERT INTO dbo.Products (Id, BrandId, Sku, Name, Price)
SELECT v.Id, b.Id, v.Sku, v.Name, v.Price
FROM (VALUES (1, N'Nova',     N'NB-X15', N'Ноутбук X15',     32999.00),
             (2, N'Aurix',    N'PH-S10', N'Смартфон S10',    18999.00),
             (3, N'Sonic',    N'AU-AIR', N'Навушники Air',    3499.00),
             (4, N'Gridline', N'MS-M7',  N'Миша ігрова M7',   1299.00),
             (5, N'Lumen',    N'MN-27Q', N'Монітор 27" QHD', 11999.00)) AS v (Id, BrandName, Sku, Name, Price)
JOIN dbo.Brands b ON b.Name = v.BrandName
WHERE NOT EXISTS (SELECT 1 FROM dbo.Products p WHERE p.Id = v.Id);
SET IDENTITY_INSERT dbo.Products OFF;

INSERT INTO dbo.ProductDetails (ProductId, Description, WeightGrams, WarrantyMonths, Specs)
SELECT v.ProductId, v.Description, v.WeightGrams, v.WarrantyMonths, v.Specs
FROM (VALUES (1, N'15.6" ноутбук для роботи й ігор', 1800, 24, N'{"cpu":"8 cores","ram":"16 GB","ssd":"512 GB"}'),
             (2, N'Смартфон з OLED-екраном',          180, 12, N'{"display":"6.5 OLED","battery":"5000 mAh"}'),
             (3, N'Бездротові навушники з ANC',        55, 12, N'{"bluetooth":"5.3","anc":true}'),
             (4, N'Ігрова миша 26000 DPI',             85,  6, NULL),
             (5, N'27" QHD IPS-монітор 165 Гц',      5200, 36, N'{"resolution":"2560x1440","refreshRate":165}')
     ) AS v (ProductId, Description, WeightGrams, WarrantyMonths, Specs)
WHERE NOT EXISTS (SELECT 1 FROM dbo.ProductDetails d WHERE d.ProductId = v.ProductId);

INSERT INTO dbo.ProductCategories (ProductId, CategoryId)
SELECT v.ProductId, c.Id
FROM (VALUES (1, N'laptops'), (1, N'gaming'), (2, N'smartphones'), (3, N'audio'), (3, N'accessories'),
             (4, N'accessories'), (4, N'gaming'), (5, N'monitors'), (5, N'gaming')) AS v (ProductId, Slug)
JOIN dbo.Categories c ON c.Slug = v.Slug
WHERE NOT EXISTS (SELECT 1 FROM dbo.ProductCategories pc WHERE pc.ProductId = v.ProductId AND pc.CategoryId = c.Id);

INSERT INTO dbo.ProductImages (ProductId, Url, SortOrder, IsMain)
SELECT v.ProductId, v.Url, v.SortOrder, v.IsMain
FROM (VALUES (1, N'/img/nb-x15-1.jpg', 0, 1), (1, N'/img/nb-x15-2.jpg', 1, 0),
             (2, N'/img/ph-s10-1.jpg', 0, 1), (3, N'/img/au-air-1.jpg', 0, 1),
             (4, N'/img/ms-m7-1.jpg',  0, 1), (5, N'/img/mn-27q-1.jpg', 0, 1)) AS v (ProductId, Url, SortOrder, IsMain)
WHERE NOT EXISTS (SELECT 1 FROM dbo.ProductImages i WHERE i.ProductId = v.ProductId AND i.Url = v.Url);
GO
