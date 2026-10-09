# Platform — Orders Service

Навчальний мікросервіс управління замовленнями (Лабораторна №2, «Розробка мікросервісної архітектури»).

## Опис домену

Сервіс **Orders** керує клієнтами, товарами та замовленнями. Замовлення створюється в одній транзакції: запис замовлення (репозиторій замовлень) і списання товару зі складу (репозиторій товарів). Якщо товару не вистачає, транзакція відкочується і замовлення не зберігається. Нове замовлення можна підтвердити, повторне підтвердження повертає конфлікт.

## Технології

.NET 9, ASP.NET Core Web API, Swagger, ADO.NET (`Microsoft.Data.SqlClient`), Dapper, AutoMapper, MS SQL Server, .NET Aspire.

## Структура

```
Platform.slnx
Platform.AppHost/           оркестрація Aspire (API + SQL Server)
Services/Orders/
  Orders.Domain/            моделі, доменні винятки
  Orders.Dal/               IGenericRepository<T>, BaseDapperRepository<T>, репозиторії, Unit of Work
  Orders.Bll/               сервіси, DTO, AutoMapper, бізнес-валідація
  Orders.Api/               контролери, middleware з ProblemDetails, Program.cs
```

Архітектура: **Api → Bll → Dal → Domain**. Репозиторії: `CustomerRepository` на чистому ADO.NET, `ProductRepository` і `OrderRepository` на Dapper (спільна база `BaseDapperRepository<T>`). Unit of Work тримає одне з'єднання й одну транзакцію для всіх репозиторіїв.

## Запуск через Aspire

Потрібні .NET 9 SDK і запущений Docker Desktop.

```bash
dotnet run --project Platform.AppHost
```

Відкрийте посилання на Dashboard із консолі. Ресурси `sql`, `OrdersDb` і `orders-api` мають мати статус **Running**. Swagger: `https://localhost:7125/swagger`.

## Розгортання БД

Окремих дій не потрібно. Під час старту `orders-api` виконується `DbInitializer`: він створює базу `OrdersDb`, таблиці, колонку `Stock` і початкові дані. Повторний запуск дублікатів не створює.

## Помилки

Усі помилки повертаються у форматі ProblemDetails (RFC 7807).

| Виняток | HTTP-статус |
|---|---|
| `NotFoundException` | 404 |
| `BusinessValidationException`, помилки DataAnnotations | 400 |
| `BusinessConflictException` | 409 |

## Приклади запитів

1. Створити клієнта зі спецсимволом: `POST /api/customers`
```json
   { "name": "O'Brien", "email": "obrien@example.com" }
```
   Очікується **201**.
2. Створити замовлення: `POST /api/orders`
```json
   {
     "customerId": "11111111-1111-1111-1111-111111111111",
     "items": [ { "productId": "22222222-2222-2222-2222-222222222222", "quantity": 2 } ]
   }
```
   Очікується **201**.
3. Відкат транзакції: той самий запит із `"quantity": 999`. Очікується **409**; замовлення в БД не з'являється, склад не змінюється.
4. Підтвердити замовлення: `POST /api/orders/{id}/confirm` → **200**; повторний виклик → **409**.
5. Неіснуючий Id: `GET /api/orders/00000000-0000-0000-0000-000000000001` → **404**.

## Таблиця дублювання даних

| Поле | Де зберігається | Джерело | Політика |
|---|---|---|---|
| `OrderItems.UnitPrice` | `OrderItems` | `Products.Price` на момент замовлення | Незмінний знімок, пізніше не оновлюється |
| `Orders.TotalAmount` | `Orders` | Сума `Quantity × UnitPrice` по позиціях | Обчислюється при створенні замовлення |
