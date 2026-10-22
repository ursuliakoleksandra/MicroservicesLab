# TechNest — платформа інтернет-магазину електроніки (.NET)

| Практичне заняття | Що зроблено | Розділ |
|-------------------|-------------|--------|
| № 01 | Три бази даних для мікросервісів (SQL Server ×2 + MongoDB), політика дублювання | [Частина 1](#частина-1--три-бази-даних-лаб-01) |
| № 02 | Мікросервіс «Замовлення»: DAL → BLL → Web/API (ADO.NET + Dapper), Unit of Work, Aspire | [Частина 2](#частина-2--мікросервіс-замовлення-лаб-02) |

# Частина 1 — три бази даних (Лаб. 01)


Практичне заняття № 01 «ASP.NET. Проєктування та розробка баз даних для мікросервісної архітектури».
Три самостійні БД одного інтернет-магазину електроніки: дві реляційні (SQL Server) і одна документна (MongoDB).

## 1. Предметна область і контексти

**TechNest** — інтернет-магазин електроніки. Покупці переглядають каталог, оформлюють і оплачують замовлення та залишають відгуки й обговорення під товарами. Систему розділено на три незалежні мікросервіси, кожен із власною БД і логікою.

| # | Контекст | БД | Сутності (кожна належить рівно одному контексту) |
|---|----------|----|--------------------------------------------------|
| 1 | **Замовлення** | `OrdersDb` — SQL Server (ADO.NET + Dapper, Лаб. 02) | Customers, Orders, OrderDetails, OrderItems, OrderStatusHistory, PaymentRecords, Products (лише локальна копія довідника) |
| 2 | **Каталог** | `CatalogDb` — SQL Server (EF Core, Лаб. 03) | Brands, Categories, Products, ProductDetails, ProductCategories, ProductImages |
| 3 | **Відгуки та обговорення** | `ReviewsDb` — MongoDB (Лаб. 04) | reviews, discussions, messages, productRatings |

Деталі (агрегати, життєвий цикл, карта контекстів) — у [`docs/contexts.md`](docs/contexts.md).
**Між базами немає жодного FOREIGN KEY і жодних міжбазових JOIN-ів**: `ProductId`, `customerId`, `orderId` — звичайні значення (логічні посилання).

```mermaid
flowchart LR
    O["OrdersDb (SQL Server)"] -. "ProductId, без FK" .-> C["CatalogDb (SQL Server)"]
    R["ReviewsDb (MongoDB)"] -. "productId, без FK" .-> C
    R -. "customerId, orderId, без FK" .-> O
```

## 2. Структура репозиторію

```
README.md                  опис домену, запуск (Aspire), розгортання БД, таблиця дублювання, приклади curl
docs/                      contexts.md · erd-p1.md · erd-p2.md · duplication-policy.md · mongo-documents.md · lab02-requests.http
db/p1/                     schema.sql · procedures.sql · seed.sql · checks.sql     (OrdersDb)
db/p2/                     schema.sql · seed.sql                                   (CatalogDb)
db/p3/                     collections.js · seed.js · queries.js                   (ReviewsDb, MongoDB)
Platform.slnx              одне рішення на всю платформу
Platform.AppHost/          оркестрація .NET Aspire (AppHost.cs)
Services/Orders/           мікросервіс «Замовлення» (Проєкт №1)
    Orders.Api/            контролери, Program.cs, appsettings.json, обробка помилок
    Orders.Bll/            сервіси, DTO, профілі AutoMapper
    Orders.Dal/            репозиторії (ADO.NET, Dapper), Unit of Work
    Orders.Domain/         моделі, доменні винятки
    Orders.Tests/          unit-тести BLL
```

## 3. Розгортання з нуля

### 3.1. Вимоги
* SQL Server 2019+ (або Docker), утиліта `sqlcmd`; MongoDB 6+ (або Docker), `mongosh`.
* Усі скрипти **ідемпотентні** — їх можна запускати повторно без помилок і дублікатів.

### 3.2. Сервери в Docker (за потреби)
```bash
docker run -d --name sql -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD='Your_strong_Passw0rd' -p 1433:1433 mcr.microsoft.com/mssql/server:2022-latest
docker run -d --name mongo -p 27017:27017 mongo:7
```

### 3.3. Проєкт №1 — OrdersDb (порядок важливий)
```bash
sqlcmd -S localhost -U sa -P 'Your_strong_Passw0rd' -C -I -i db/p1/schema.sql      # 1) БД, таблиці, індекси, view
sqlcmd -S localhost -U sa -P 'Your_strong_Passw0rd' -C -I -i db/p1/procedures.sql  # 2) 6 збережуваних процедур
sqlcmd -S localhost -U sa -P 'Your_strong_Passw0rd' -C -I -i db/p1/seed.sql        # 3) тестові дані (ідемпотентно)
sqlcmd -S localhost -U sa -P 'Your_strong_Passw0rd' -C -I -i db/p1/checks.sql      # 4) перевірки (CRUD, помилки, конкурентність)
```
`-I` обов'язковий (QUOTED_IDENTIFIER для фільтрованих індексів). У SSMS достатньо відкрити файли й виконати по черзі.

### 3.4. Проєкт №2 — CatalogDb
```bash
sqlcmd -S localhost -U sa -P 'Your_strong_Passw0rd' -C -I -i db/p2/schema.sql
sqlcmd -S localhost -U sa -P 'Your_strong_Passw0rd' -C -I -i db/p2/seed.sql
```

### 3.5. Проєкт №3 — ReviewsDb (MongoDB)
```bash
mongosh "mongodb://localhost:27017" --file db/p3/collections.js   # колекції, JSON Schema, індекси
mongosh "mongodb://localhost:27017" --file db/p3/seed.js          # дані (upsert, ідемпотентно)
mongosh "mongodb://localhost:27017" --file db/p3/queries.js       # find / aggregation / $lookup / $text
```

### 3.6. Самоперевірка
| Що перевірити | Як |
|---------------|----|
| Немає FK за межі бази | `SELECT * FROM sys.foreign_keys` у `OrdersDb` і `CatalogDb` — усі таблиці лише своєї БД (також `checks.sql`, п. 1) |
| Недопустимий перехід статусу | `checks.sql`, п. 5–6: «відвантажити» неоплачене → помилка **50002**, статус не змінився |
| Конкурентність | `checks.sql`, п. 7: застарілий `RowVersion` → помилка **50008** |
| Різні набори полів у MongoDB | `db.reviews.find()` у `ReviewsDb` — документи мають різні поля (photos / pros+cons / videoUrl / лише rating) |
| Ідемпотентність seed | Запустити `seed.sql` / `seed.js` вдруге — кількість рядків/документів не змінюється |

### 3.7. (Рекомендовано) Принцип найменших привілеїв
```sql
-- виконати в master:  CREATE LOGIN orders_app WITH PASSWORD = '<пароль>';
USE OrdersDb;  CREATE USER orders_app FOR LOGIN orders_app;
GRANT SELECT, INSERT, UPDATE, EXECUTE ON SCHEMA::dbo TO orders_app;   -- доступ лише до власної БД, без DELETE/DDL
```

## 4. ERD

### 4.1. Проєкт №1 — OrdersDb
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
        int Stock "залишок для замовлень"
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

### 4.2. Проєкт №2 — CatalogDb
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

### 4.3. Проєкт №3 — ReviewsDb (MongoDB)
```
reviews ──(embedded)── author{}, comments[], verifiedPurchase{}, photos[]      ← EMBEDDING
reviews ──(reference)── discussionId ─► discussions                            ← REFERENCING (гібрид)
messages ──(reference)── discussionId ─► discussions, parentMessageId ─► messages
productRatings (_id = productId)                                                ← проєкція для читання
```
Приклади документів — [`docs/mongo-documents.md`](docs/mongo-documents.md).

## 5. Типи зв'язків

| БД | 1:1 | 1:N | M:N |
|----|-----|-----|-----|
| OrdersDb | Orders ↔ OrderDetails (FK = PK) | Customers → Orders; Orders → OrderStatusHistory / PaymentRecords | Orders ↔ Products через OrderItems |
| CatalogDb | Products ↔ ProductDetails (FK = PK) | Brands → Products; Products → ProductImages | Products ↔ Categories через ProductCategories (власне поле `AddedAt`) |

## 6. Ключі, обмеження, індекси

* **PK:** `BIGINT IDENTITY` — вставки йдуть з одного джерела (одна БД на сервіс), ключ компактний і послідовний. GUID був би виправданий, якби Id генерувались у кількох місцях. `OrdersDb.Products.Id` — не IDENTITY, а Id із Каталогу. У MongoDB — `ObjectId`.
* **Обмеження:** PK, FK, UNIQUE (`Customers.Email`, `Products.Sku`, …), CHECK (статуси, `Price > 0`, `Units > 0`, `Discount <= UnitPrice*Units`, `ISJSON`), DEFAULT.
* **Каскади:** `ON DELETE CASCADE` лише для залежних частин агрегата (OrderDetails, OrderItems, OrderStatusHistory, ProductDetails, ProductImages, ProductCategories). Для клієнта з історією замовлень і платежів каскаду **немає**.
* **Індекси** — кожен має коментар у `schema.sql` з описом запиту: `IX_Orders_CustomerId_Status`, `IX_Orders_Active` (filtered), `IX_Orders_Status_OrderDate` (filtered), `IX_OrderItems_ProductId`, `IX_OrderStatusHistory_OrderId_ChangedAt`, `IX_PaymentRecords_OrderId`, `IX_Products_BrandId`, `IX_Products_Name_Active` (filtered), `IX_ProductCategories_CategoryId`, `IX_ProductImages_ProductId_SortOrder`, `UX_ProductImages_Main` (unique filtered); у MongoDB — див. `collections.js`.
* **Аудит і soft delete:** `CreatedAt/CreatedBy/UpdatedAt/UpdatedBy/IsDeleted` у `Customers` та `Orders`.
* **Конкурентність:** `ROWVERSION` у `Orders` і `Customers` — оптимістичне блокування: два користувачі, що редагують одне замовлення, не затруть зміни «тихо» (`usp_UpdateOrderDetails` повертає помилку 50008 при застарілому RowVersion). Під час зміни статусу додатково використовується `UPDLOCK` у транзакції.

## 7. Збережувані процедури Проєкту №1

| Процедура | Призначення |
|-----------|-------------|
| `usp_CreateOrder` | Create: замовлення + деталі + позиції (знімок назви/ціни) + запис в історії, у транзакції |
| `usp_GetOrderById` | Read: 3 набори (шапка + RowVersion, позиції, історія статусів) |
| `usp_GetCustomerOrders` | Read: фільтр за статусом і пагінація (OFFSET/FETCH) |
| `usp_UpdateOrderDetails` | Update: адреса/коментар з перевіркою RowVersion |
| `usp_SoftDeleteOrder` | Delete: м'яке видалення (лише Submitted/Cancelled) |
| `usp_ChangeOrderStatus` | **Транзакційна** зміна статусу: `UPDLOCK`, перевірка переходу, `THROW 50002` на недопустимому, запис у PaymentRecords при `Paid` та в історію |

Допустимі переходи: `Submitted → AwaitingValidation → StockConfirmed → Paid → Shipped`; `Cancelled` — лише до оплати.
Коди керованих помилок наведено на початку `procedures.sql`.

## 8. Політика дублювання даних і узгодженості

| Поле | Де зберігається | Джерело (власник даних) | Політика узгодженості |
|------|-----------------|-------------------------|-----------------------|
| `OrderItems.ProductName`, `OrderItems.UnitPrice` | OrdersDb | Product (CatalogDb) | **Знімок** на момент оформлення — не оновлюється |
| `Products.Name`, `Products.Price`, `Products.IsActive` (локальна копія довідника) | OrdersDb | Product (CatalogDb) | Копія для читання, **оновлюється подіями** з Каталогу (eventual consistency, Лаб. 08) |
| `reviews.productName` | ReviewsDb (MongoDB) | Product (CatalogDb) | Копія для читання, **оновлюється подіями** з Каталогу |
| `discussions.productName` | ReviewsDb (MongoDB) | Product (CatalogDb) | Копія для читання, **оновлюється подіями** з Каталогу |
| `productRatings.productName` | ReviewsDb (MongoDB) | Product (CatalogDb) | Копія для читання, **оновлюється подіями** з Каталогу |
| `reviews.author.displayName`, `messages.author.displayName` | ReviewsDb (MongoDB) | Customer (OrdersDb, `Customers.FullName`) | **Знімок** на момент написання — не оновлюється |
| `reviews.verifiedPurchase.orderId` | ReviewsDb (MongoDB) | Order (OrdersDb) | Лише посилання за Id (без FK); факт покупки фіксується на момент написання відгуку |
| `Products.Stock` (OrdersDb) | OrdersDb | Сервіс Замовлень (резервування залишку при підтвердженні) | Власник — Замовлення: зменшується при `confirm`, повертається при `cancel`; поповнення приходить подіями (Лаб. 08). Каталог цього поля не редагує |

* **Знімок** — свідомо не оновлюється (ціна й назва в позиції замовлення фіксуються на момент покупки, як `OrderItem` в Microsoft eShop).
* **Копія для читання** — оновлюється подіями від власника даних (механізм подій — Лаб. 08; до того копії змінюються лише скриптами від імені власника).
* Незалежне редагування двох копій без власника (**dual write**) заборонене.
* Жодне з цих полів не має FOREIGN KEY на іншу базу.

## 9. Відповідність вимогам

| Вимога | Де |
|--------|----|
| Опис домену та 3 контексти | цей README, `docs/contexts.md` |
| Таблиця політики дублювання | розділ 8, `docs/duplication-policy.md` |
| Три бази без FK між ними | розділ 3.6, `db/` |
| P1: ≥4 таблиці, 1:1 / 1:N / M:N | `db/p1/schema.sql` (7 таблиць) |
| Ключі, обмеження, індекси під запити | `db/p1/schema.sql`, `db/p2/schema.sql` |
| Аудит, soft delete, rowversion | `db/p1/schema.sql` |
| 4–6 процедур, транзакційна зміна статусу | `db/p1/procedures.sql` (6 процедур) |
| P2: 6 сутностей, M:N з власним полем | `db/p2/schema.sql`, `db/p2/seed.sql` |
| P3: 4 колекції, embed / reference / гібрид, гнучка схема | `db/p3/collections.js`, `db/p3/seed.js`, `docs/mongo-documents.md` |
| Ідемпотентні seed-скрипти | `db/p1/seed.sql`, `db/p2/seed.sql`, `db/p3/seed.js` |
| Скрипти розгортання, README, ERD | розділи 3–4 |

---

# Частина 2 — мікросервіс «Замовлення» (Лаб. 02)

Тришарова архітектура **Web/API → BLL → DAL** поверх бази `OrdersDb` (Проєкт №1 з Лаб. 01): репозиторії на **ADO.NET** та **Dapper**, **Unit of Work** з транзакціями, **DTO + AutoMapper**, асинхронні контролери, помилки у форматі **ProblemDetails**, запуск через **.NET Aspire**.

## 11. Структура рішення та залежності

| Проєкт | Призначення |
|--------|-------------|
| `Orders.Api` | Web/API: контролери, Program.cs (DI, Serilog, Swagger), глобальний обробник помилок |
| `Orders.Bll` | сервіси й інтерфейси, DTO, профілі AutoMapper, бізнес-валідація |
| `Orders.Dal` | інтерфейси репозиторіїв (у т.ч. `IGenericRepository<T>`), реалізації ADO.NET / Dapper, Unit of Work |
| `Orders.Domain` | моделі, константи статусів, доменні винятки (`NotFound`, `BusinessConflict`, `Validation`) |
| `Orders.Tests` | unit-тести BLL (NSubstitute) і перевірка конфігурації AutoMapper |
| `Platform.AppHost` | .NET Aspire: запуск API разом із базою даних |

Залежності спрямовані в один бік: `Api → Bll → Dal`; `Domain` підключають і `Dal`, і `Bll`. Контролер не звертається до репозиторію — лише до сервісу; SQL є **лише** в `Orders.Dal`.

```mermaid
flowchart LR
    C["Orders.Api<br/>контролери"] -- DTO --> B["Orders.Bll<br/>сервіси, AutoMapper"]
    B -- "доменні моделі" --> D["Orders.Dal<br/>репозиторії, UnitOfWork"]
    D -- SQL --> S[("OrdersDb<br/>SQL Server")]
    C -. "заборонено: минаючи BLL" .-> D
    M["Orders.Domain<br/>моделі, винятки"] --- B
    M --- D
```

### Репозиторії (DAL)

| Репозиторій | Технологія | Особливості |
|-------------|-----------|-------------|
| `CustomerRepository` | **чистий ADO.NET** (`SqlCommand`, `SqlParameter`, `SqlDataReader`) | свідомо **не** успадковує `IGenericRepository<T>`; власні `Update`/`SoftDelete`; ручне читання через reader |
| `BaseDapperRepository<T>` | Dapper | спільний CRUD **написано один раз** (`GetById`, `GetAll`, `Add`, `Delete`); `SELECT`/`INSERT` будуються з властивостей моделі |
| `OrderRepository : BaseDapperRepository<Order>` | Dapper | **multi-mapping** (`Order` + `OrderItem` одним JOIN), виклик збережених процедур Лаб. 01 (`usp_ChangeOrderStatus`, `usp_GetCustomerOrders`, `usp_UpdateOrderDetails`, `usp_SoftDeleteOrder`), асинхронні `QueryAsync`/`ExecuteAsync` |
| `ProductRepository : BaseDapperRepository<Product>` | Dapper | `IN @Ids`, атомарне `DecreaseStock` (`WHERE Stock >= @Quantity`), `Upsert` локальної копії |

* **Параметризація:** значення передаються **лише** як параметри (`SqlParameter` / об'єкт-параметри Dapper). Ім'я таблиці в `BaseDapperRepository` — константа з коду і додатково перевіряється регулярним виразом.
* **З'єднання:** `await using` / `DisposeAsync`; пул підключень SqlClient увімкнено за замовчуванням. Dapper не керує з'єднанням — лише розширює `SqlConnection`.
* **Перетворення помилок БД** (`SqlErrorTranslator`): `THROW 50001` → `NotFoundException`; `50002` (недопустимий перехід) → `BusinessConflictException`; `2601/2627` (UNIQUE) → `BusinessConflictException`. Raw SQL-повідомлення в API не потрапляють.

### Unit of Work

`UnitOfWork` відкриває **одне** `SqlConnection`, створює всі репозиторії з цим з'єднанням і функцією `() => _transaction`, тому всі вони автоматично працюють у **поточній** транзакції (`BeginTransactionAsync(IsolationLevel)` → `CommitAsync` / `RollbackAsync` → `DisposeAsync`). Реєструється як **Scoped**: один екземпляр і одне з'єднання на HTTP-запит.

```mermaid
sequenceDiagram
    participant S as OrderService
    participant U as UnitOfWork
    participant O as OrderRepository
    participant P as ProductRepository
    participant DB as SQL Server
    S->>U: BeginTransactionAsync(ReadCommitted)
    S->>O: ChangeStatusAsync(StockConfirmed)
    O->>DB: EXEC usp_ChangeOrderStatus
    S->>P: DecreaseStockAsync(productId, units)
    P->>DB: UPDATE Products SET Stock = Stock - @q WHERE Stock >= @q
    alt обидві операції успішні
        S->>U: CommitAsync()
    else хоча б одна впала
        S->>U: RollbackAsync() — без часткових змін
    end
```

**Рівень ізоляції та таймаути.** За замовчуванням `ReadCommitted`: немає «брудних» читань і мінімум блокувань. Вищі рівні (`RepeatableRead`, `Serializable`) прибирають більше аномалій читання, але тримають більше блокувань і підвищують ризик взаємних блокувань (deadlock); нижчі — швидші, але допускають аномалії. Критичну консистентність забезпечено точково: `UPDLOCK` у процедурі зміни статусу та умова `Stock >= @Quantity` в одному `UPDATE`. Для зменшення ризику deadlock залишки змінюються у **фіксованому порядку** (за `ProductId`). Таймаути: підключення — `Connect Timeout=15` у рядку підключення; команди — 30 с (`RepositoryBase.CommandTimeoutSeconds`).

**Відкат, ініційований сервером.** Процедура з Лаб. 01 працює з `XACT_ABORT ON`: після `THROW` SQL Server сам відкочує *всю* транзакцію. Тому `UnitOfWork.RollbackAsync` толерантний до вже завершеної транзакції (ловить `InvalidOperationException`/`SqlException`).

### Бізнес-операції (BLL) і валідація

| Метод сервісу | Транзакція | Що змінює |
|---------------|-----------|-----------|
| `CreateAsync` | так | `Orders` + `OrderDetails` + `OrderItems` + `OrderStatusHistory` (назва й ціна позиції беруться з `Products` як **знімок**) |
| `ConfirmOrderAsync` | так | статус замовлення → `StockConfirmed` та зменшення `Products.Stock` — або обидва, або жодне |
| `CancelOrderAsync` | так | статус → `Cancelled` + повернення залишку, якщо замовлення було підтверджене |
| `PayOrderAsync`, `ShipOrderAsync` | процедура | `usp_ChangeOrderStatus` (власна транзакція); `Shipped` для неоплаченого → 409 |

Валідація на двох рівнях: **DataAnnotations** на DTO (відхиляє некоректний запит ще до сервісу → 400) і **бізнес-правила в BLL** (клієнт/товар існує, дублікати позицій, допустимі статуси, page/pageSize, унікальність email).

### Відповідність помилок і HTTP-статусів

| Де виникає | Виняток (Domain) | HTTP |
|------------|------------------|------|
| `THROW 50001/50006/50010`, сервіс не знайшов ресурс | `NotFoundException` | **404** |
| `THROW 50002/50004/50008/50009`, UNIQUE (2601/2627), нестача товару | `BusinessConflictException` | **409** |
| `THROW 50003/50005/50007`, бізнес-валідація BLL | `ValidationException` | **400** |
| DataAnnotations на DTO (до сервісу) | автоматично `[ApiController]` | **400** (ValidationProblemDetails) |
| інше | — | **500** (без технічних деталей) |

Усе перетворюється **одним** глобальним обробником `DomainExceptionHandler` (`IExceptionHandler` + `AddProblemDetails`); у кожну відповідь додається `traceId`. Операції логуються Serilog з кореляційним ідентифікатором (`X-Correlation-ID`), без персональних і секретних даних.

## 12. Запуск через .NET Aspire

**Потрібно:** .NET SDK 9.0.200+ (формат `.slnx`), SQL Server з базою з Лаб. 01. Docker потрібен лише для SQL Server у контейнері (або для варіанта 2 нижче).

1. **Розгорніть БД** (розділ 3.3 цього README; після зміни схеми в Лаб. 02 **перезапустіть** `schema.sql` → `procedures.sql` → `seed.sql` — у `Products` додано колонку `Stock`, скрипти ідемпотентні й самі мігрують стару схему).
2. **Вкажіть рядок підключення** у `Platform.AppHost/appsettings.json` (секція `ConnectionStrings:OrdersDb`; за замовчуванням — `sa` з Docker-команди розділу 3.2). Для запуску API без Aspire — той самий ключ у `Services/Orders/Orders.Api/appsettings.json` або змінна оточення `ConnectionStrings__OrdersDb=Server=...;Database=OrdersDb;...`.
3. **Запустіть однією командою:**
   ```bash
   dotnet run --project Platform.AppHost
   ```
4. Відкрийте **Aspire Dashboard** за посиланням з консолі: ресурс `orders-api` має статус **Running**. Натисніть на його endpoint — відкриється **Swagger** (`/` перенаправляє на `/swagger`).
5. Візьміть URL API з Dashboard і виконайте запити зі Swagger або `curl` (розділ 13).

**Варіант 2 — SQL Server у контейнері:** у `Platform.AppHost/AppHost.cs` закоментуйте `AddConnectionString`, розкоментуйте `AddSqlServer("sql").WithDataVolume().AddDatabase("OrdersDb")`. База створюється **порожньою** — застосуйте до неї `db/p1/schema.sql`, `procedures.sql`, `seed.sql` (рядок підключення видно в Dashboard).

Ім'я ресурсу `OrdersDb` збігається з іменем рядка підключення в API, тому Aspire передає його через `WithReference(ordersDb)` автоматично (`ConnectionStrings__OrdersDb`), і код `Orders.Api` не змінюється.

Без Aspire: `dotnet run --project Services/Orders/Orders.Api` → <http://localhost:5080/swagger>. Тести: `dotnet test Services/Orders/Orders.Tests`.

## 13. Приклади запитів (curl)

Підставте адресу з Aspire Dashboard (або `http://localhost:5080`). Ті самі запити — у [`docs/lab02-requests.http`](docs/lab02-requests.http). Для `https` з dev-сертифікатом додайте `-k`. Дані — з `seed.sql` (замовлення 1 — Paid, 2 — Submitted, 3 — Cancelled).

```bash
API=http://localhost:5080

# --- READ -----------------------------------------------------------------
curl -i $API/api/products
curl -i $API/api/customers/1
curl -i "$API/api/orders?customerId=1&status=Paid&page=1&pageSize=5"   # фільтр + пагінація
curl -i $API/api/orders/2                                               # замовлення з позиціями та rowVersion
curl -i $API/api/orders/2/history

# --- CREATE (201 Created + Location) ---------------------------------------
curl -i -X POST $API/api/orders -H "Content-Type: application/json" \
  -d '{"customerId":1,"shippingAddress":"Чернівці, вул. Руська, 10","items":[{"productId":3,"units":2},{"productId":4,"units":1}]}'

# спецсимвол у даних (SQL-ін'єкція неможлива: усе — параметри)
curl -i -X POST $API/api/customers -H "Content-Type: application/json" \
  -d "{\"email\":\"patrick.obrien@example.com\",\"fullName\":\"Patrick O'Brien\"}"

# --- ПОМИЛКИ (ProblemDetails) ----------------------------------------------
curl -i -X POST $API/api/orders/2/ship          # 409: Invalid status transition: Submitted -> Shipped
curl -i -X POST $API/api/orders/999999/confirm  # 404: Order 999999 not found
curl -i -X POST $API/api/orders -H "Content-Type: application/json" -d '{"customerId":0,"shippingAddress":"","items":[]}'   # 400

# --- ТРАНЗАКЦІЙНА БІЗНЕС-ОПЕРАЦІЯ ------------------------------------------
curl -i -X POST $API/api/orders/2/confirm       # 204: статус -> StockConfirmed, залишки товарів 2 і 3 зменшено
curl -i -X POST $API/api/orders/2/confirm       # 409: повторне підтвердження
curl -i -X POST $API/api/orders/2/pay -H "Content-Type: application/json" -d '{"paymentMethod":"Card"}'   # 204
curl -i -X POST $API/api/orders/2/ship          # 204: тепер дозволено

# --- ВІДКАТ ТРАНЗАКЦІЇ ------------------------------------------------------
# товар 5 має залишок 5; замовляємо 1 шт. товару 4 і 6 шт. товару 5
curl -i -X POST $API/api/orders -H "Content-Type: application/json" \
  -d '{"customerId":2,"shippingAddress":"Київ, просп. Науки, 1","items":[{"productId":4,"units":1},{"productId":5,"units":6}]}'
curl -i -X POST $API/api/orders/5/confirm       # 409 Insufficient stock for product 5
curl -s $API/api/orders/5                       # статус усе ще Submitted -> зміну статусу відкочено
curl -s $API/api/products/4                     # залишок товару 4 не змінився -> відкотилось і перше списання

# --- ОПТИМІСТИЧНА КОНКУРЕНТНІСТЬ -------------------------------------------
curl -s $API/api/orders/4                       # скопіюйте поле rowVersion
curl -i -X PUT $API/api/orders/4/details -H "Content-Type: application/json" \
  -d '{"shippingAddress":"Нова адреса, 5","comment":"змінено","rowVersion":"<rowVersion>"}'   # 200
# повторіть той самий запит зі СТАРИМ rowVersion -> 409 Concurrency conflict
```

Приклад відповіді 409 (`application/problem+json`):
```json
{ "title": "Business rule conflict", "status": 409,
  "detail": "Invalid status transition: Submitted -> Shipped", "traceId": "0HN..." }
```

| Метод і шлях | Успіх | Помилки |
|--------------|-------|---------|
| `GET /api/orders/{id}` | 200 | 404 |
| `GET /api/orders?customerId=&status=&page=&pageSize=` | 200 | 400, 404 |
| `GET /api/orders/{id}/history` | 200 | 404 |
| `POST /api/orders` | **201** + `Location` | 400 |
| `PUT /api/orders/{id}/details` | 200 | 400, 404, 409 |
| `DELETE /api/orders/{id}` (soft) | 204 | 404, 409 |
| `POST /api/orders/{id}/confirm` · `/pay` · `/ship` · `/cancel` | 204 | 400, 404, 409 |
| `GET/POST/PUT/DELETE /api/customers` | 200 / 201 / 200 / 204 | 400, 404, 409 |
| `GET /api/products`, `GET /api/products/{id}`, `PUT /api/products/{id}` (sync з Каталогу) | 200 | 400, 404 |

## 14. Git

Гілки: `main` — стабільна, `develop` — інтеграційна, `feature/*` — завдання (наприклад `feature/dal`, `feature/bll`, `feature/api`, `feature/aspire`). Зміни зливаються в `develop` через Pull Request з описом, `develop` → `main` — після перевірки.

```bash
git init -b main && git add . && git commit -m "chore: Lab 01 databases"
git switch -c develop
git switch -c feature/dal    # ... комітимо: feat(dal): ADO.NET CustomerRepository, Dapper repositories, UnitOfWork
```

## 15. Самоперевірка перед здачею

| Перевірка | Очікуваний результат |
|-----------|----------------------|
| Створити клієнта з ім'ям `O'Brien` | 201, без синтаксичної помилки SQL |
| Підтвердити замовлення, де товару не вистачає (розділ 13, «Відкат») | 409, статус і залишок першого товару не змінилися |
| Підтвердити вже підтверджене замовлення | 409 у форматі ProblemDetails |
| `GET /api/orders/999999` | 404, не 500 |
| `dotnet run --project Platform.AppHost` | у Dashboard `orders-api` — Running; Swagger відкривається |
| `dotnet test Services/Orders/Orders.Tests` | усі тести зелені |

**Знімки екрана для здачі:** Swagger з переліком ендпоінтів і Aspire Dashboard зі статусом Running (покласти в `docs/screenshots/`).

## 16. Відповідність критеріям Лаб. 02

| Критерій | Де |
|----------|----|
| Рішення з шарів Api → Bll → Dal (+ Domain), DI, рядок підключення в appsettings, Scoped UoW, логування | `Platform.slnx`, `Orders.Api/Program.cs`, `appsettings.json` |
| `IGenericRepository<T>` + конкретні інтерфейси | `Orders.Dal/Interfaces/` |
| Репозиторій на чистому ADO.NET (без Generic Repository) | `CustomerRepository` |
| ≥ 2 репозиторії на Dapper зі спільною `BaseDapperRepository<T>` | `OrderRepository`, `ProductRepository` |
| Усі запити параметризовані, з'єднання звільняються | `Orders.Dal/Repositories/` |
| Unit of Work: одне з'єднання, транзакція з рівнем ізоляції, Commit/Rollback/DisposeAsync | `Orders.Dal/UnitOfWork.cs` |
| DTO + профілі AutoMapper в BLL, зареєстровані в DI | `Orders.Bll/Dtos`, `Mapping`, `Program.cs` |
| Транзакційна операція через кілька репозиторіїв, знімок назви й ціни | `OrderService.CreateAsync` / `ConfirmOrderAsync` / `CancelOrderAsync` |
| Доменні винятки NotFound / BusinessConflict / Validation | `Orders.Domain/Exceptions` |
| Валідація: DataAnnotations (DTO) + бізнес-правила (BLL) | `Dtos/*.cs`, `Services/*.cs` |
| Тонкі асинхронні контролери, атрибутна маршрутизація, Swagger, `CreatedAtAction`, 204, `CancellationToken` | `Orders.Api/Controllers` |
| Централізована обробка помилок у ProblemDetails | `ErrorHandling/DomainExceptionHandler.cs` |
| AppHost запускає API разом із базою даних | `Platform.AppHost/AppHost.cs` |
| README із запуском через Aspire та прикладами curl | розділи 12–13 |
