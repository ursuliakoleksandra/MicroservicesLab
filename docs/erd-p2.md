# ERD — Проєкт №2: CatalogDb (SQL Server, EF Core Code First)

Зв'язки: **1:1** Products ↔ ProductDetails (FK є PK); **1:N** Brands → Products, Products → ProductImages; **M:N** Products ↔ Categories через ProductCategories з власним полем `AddedAt`.

```mermaid
erDiagram
    BRANDS ||--o{ PRODUCTS : "1:N (Products.BrandId)"
    PRODUCTS ||--|| PRODUCT_DETAILS : "1:1 (FK = PK)"
    PRODUCTS ||--o{ PRODUCT_IMAGES : "1:N CASCADE"
    PRODUCTS ||--o{ PRODUCT_CATEGORIES : "M:N"
    CATEGORIES ||--o{ PRODUCT_CATEGORIES : "M:N"

    BRANDS {
        bigint Id PK "IDENTITY"
        nvarchar Name UK
        nvarchar Country
    }
    CATEGORIES {
        bigint Id PK "IDENTITY"
        nvarchar Name UK
        nvarchar Slug UK
    }
    PRODUCTS {
        bigint Id PK "IDENTITY"
        bigint BrandId FK
        nvarchar Sku UK
        nvarchar Name "індекс пошуку"
        decimal Price "CHECK > 0"
        bit IsActive
        datetime2 CreatedAt
        datetime2 UpdatedAt
    }
    PRODUCT_DETAILS {
        bigint ProductId PK "PK + FK"
        nvarchar Description
        int WeightGrams "CHECK > 0"
        int WarrantyMonths "DEFAULT 12"
        nvarchar Specs "CHECK ISJSON"
    }
    PRODUCT_CATEGORIES {
        bigint ProductId PK "PK + FK"
        bigint CategoryId PK "PK + FK"
        datetime2 AddedAt "власне поле M:N"
    }
    PRODUCT_IMAGES {
        bigint Id PK
        bigint ProductId FK
        nvarchar Url
        int SortOrder
        bit IsMain "унікальний головний"
    }
```
