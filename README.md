# MicroservicesLab — Orders Vertical Slice API

Навчальний мікросервіс управління замовленнями (**Orders Service**), реалізований у рамках Практичної роботи №2 з дисципліни «Розробка мікросервісної архітектури».

Проєкт побудовано за принципами **Vertical Slice Architecture** та паттерну **Unit of Work / Repository**.

---

## 🛠 Технологічний стек

* **Платформа:** .NET 9 / C# 13
* **API Framework:** ASP.NET Core Web API, Swagger / OpenAPI
* **Data Access Layer (DAL):** ADO.NET (`Microsoft.Data.SqlClient`), Dapper
* **Обробка помилок:** Middleware з підтримкою стандарту `ProblemDetails` (RFC 7807)
* **Мапінг об'єктів:** AutoMapper
* **База даних:** MS SQL Server (у Docker або локально)

---

## 🏛 Архітектура проєкту

Проєкт розділено на чіткі шари відповідно до вертикального зрізу:

1. **`Orders.Domain`** — Доменні моделі (`Order`, `OrderItem`, `Customer`, `Product`), переліки (`OrderStatus`) та спеціалізовані винятки (`NotFoundException`, `BusinessValidationException`).
2. **`Orders.Dal`** — Шар доступу до даних:
   * **`CustomerRepository`** — реалізація на чистому **ADO.NET**.
   * **`ProductRepository`** та **`OrderRepository`** — реалізації з використанням **Dapper**.
   * **`UnitOfWork`** — управління транзакціями SQL та єдина точка доступу до репозиторіїв.
   * **`DbInitializer`** — автоматичне створення бази даних, схеми таблиць та заповнення початковими даними (Seed Data).
3. **`Orders.Bll`** — Бізнес-логіка: DTOs (`CreateOrderDto`, `OrderDto`), профілі `AutoMapper` та `OrderService`, що забезпечує валідацію інваріантів і створення замовлень у транзакції.
4. **`Orders.Api`** — Web API: тонкий контролер `OrdersController`, реєстрація залежностей (DI) та `ExceptionHandlingMiddleware` для форматування помилок.

---

## 🚀 Інструкція із запуску

### 1. Передумови
* Встановлений **.NET 9 SDK**.
* Запущений **MS SQL Server** (наприклад, через Docker-контейнер на порту `1433`).

### 2. Конфігурація підключення
Перевірте рядок підключення у файлі `Orders.Api/appsettings.json`:
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost,1433;Database=OrdersDb;User Id=sa;Password=YourPassword123!;TrustServerCertificate=True;"
}
### 3. Запуск застосунку
У терміналі виконайте:
dotnet run --project Orders.Api
