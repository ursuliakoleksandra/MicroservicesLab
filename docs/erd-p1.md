# ERD — Проєкт №1: OrdersDb (SQL Server)

Зв'язки: **1:1** Orders ↔ OrderDetails (FK є одночасно PK); **1:N** Customers → Orders, Orders → OrderStatusHistory, Orders → PaymentRecords; **M:N** Orders ↔ Products через OrderItems.
`Products` — локальна копія довідника Каталогу (Id збігається з CatalogDb, FK на Каталог немає).

```mermaid
erDiagram
    CUSTOMERS ||--o{ ORDERS : "1:N (Orders.CustomerId)"
    ORDERS ||--|| ORDER_DETAILS : "1:1 (FK = PK)"
    ORDERS ||--|{ ORDER_ITEMS : "1:N CASCADE"
    PRODUCTS ||--o{ ORDER_ITEMS : "M:N Orders-Products"
    ORDERS ||--o{ ORDER_STATUS_HISTORY : "1:N аудит статусів"
    ORDERS ||--o{ PAYMENT_RECORDS : "1:N"

    CUSTOMERS {
        bigint Id PK "IDENTITY"
        nvarchar Email UK "UNIQUE"
        nvarchar FullName
        nvarchar Phone
        datetime2 CreatedAt
        bit IsDeleted
        rowversion RowVersion
    }
    ORDERS {
        bigint Id PK "IDENTITY"
        bigint CustomerId FK
        nvarchar Status "CHECK, DEFAULT Submitted"
        datetime2 OrderDate
        datetime2 CreatedAt
        nvarchar CreatedBy
        datetime2 UpdatedAt
        nvarchar UpdatedBy
        bit IsDeleted
        rowversion RowVersion
    }
    ORDER_DETAILS {
        bigint OrderId PK "PK + FK to Orders"
        nvarchar ShippingAddress
        nvarchar Comment
    }
    PRODUCTS {
        bigint Id PK "Id з CatalogDb, не IDENTITY"
        nvarchar Name "копія"
        decimal Price "CHECK > 0, копія"
        bit IsActive
        datetime2 SyncedAt
    }
    ORDER_ITEMS {
        bigint OrderId PK "PK + FK"
        bigint ProductId PK "PK + FK to Products"
        nvarchar ProductName "знімок"
        decimal UnitPrice "знімок"
        decimal Discount
        int Units "CHECK > 0"
    }
    ORDER_STATUS_HISTORY {
        bigint Id PK
        bigint OrderId FK
        nvarchar OldStatus
        nvarchar NewStatus
        datetime2 ChangedAt
        nvarchar ChangedBy
    }
    PAYMENT_RECORDS {
        bigint Id PK
        bigint OrderId FK
        decimal Amount "CHECK > 0"
        nvarchar Method "CHECK"
        datetime2 PaidAt
    }
```
