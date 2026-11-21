# Перевірка Лаб. 04 (Reviews) перед інтеграцією в Лаб. 05

Компілятор .NET у середовищі перевірки був недоступний, тому перевірка — читанням коду шар за шаром
(Domain → Application → Infrastructure → WebApi → тести) і зіставленням із Лаб. 02–03 та вимогами Лаб. 05.

## Блокуючих помилок у самому сервісі не знайдено
Залежності між шарами односпрямовані, BSON-мапінг, CQRS-pipeline, оптимістична конкурентність, індекси
(імена збігаються з `db/p3/collections.js`) і сідінг узгоджені між собою.

## Знайдені проблеми

| # | Де | Проблема | Статус |
|---|----|----------|--------|
| 1 | `Reviews.WebApi/ErrorHandling/ApiExceptionHandler.cs` | Виняток зіставлявся як `MongoBulkWriteException<BsonDocument>`, але `InsertManyAsync<Review>` кидає `MongoBulkWriteException<Review>` — гілка ніколи не спрацьовувала б (500 замість 400). | **Виправлено**: зіставлення за негенеричним `MongoBulkWriteException` |
| 2 | `Platform.AppHost/AppHost.cs` (Лаб. 04) | Ресурс названо `reviews-webapi`, а Лаб. 05 очікує логічне ім'я `reviews-api` (service discovery, YARP, Aggregator). | **Виправлено** в Лаб. 05 |
| 3 | `Reviews.WebApi/Program.cs` | Власний `MapHealthChecks("/health")` конфліктував би з `MapDefaultEndpoints()` (два ендпоінти з одним маршрутом → `AmbiguousMatchException`). | **Виправлено**: перевірка MongoDB лишилась у загальному `/health` |
| 4 | Reviews, Orders, Catalog | Три різні реалізації CorrelationId (`X-Correlation-ID` / `RequestLogContextMiddleware`) і три окремі конфігурації Serilog — суперечать вимозі Лаб. 05 про єдині ServiceDefaults. | **Замінено** спільними |
| 5 | Orders та Catalog | Обидва сервіси мають `/api/products` (в Orders — локальна копія довідника). Gateway не може маршрутизувати один шлях у два сервіси. | **Обійдено** без зміни коду сервісів: `/api/products` → Каталог, `/api/order-products` → Замовлення (YARP `PathPattern`) |
| 6 | Reviews, `MongoDbContext` | Команди MongoDB не потрапляли в trace (власний `MongoClient`). | **Виправлено**: `DiagnosticsActivityEventSubscriber` |
| 7 | `OrdersDb` у контейнері | `AddDatabase("OrdersDb")` створює порожню БД, а схема Лаб. 01 лежить у `db/p1`. Без неї `orders-api` падає на першому запиті. | **Виправлено**: `Platform.DbInit` застосовує скрипти, `orders-api` чекає `WaitForCompletion` |

## Зауваження без змін у коді
* `ReviewsController.GetList` не обмежує `pageSize` у контролері (обмежує валідатор: 1…100) — це коректно, лише відзначено.
* `MediatR`/`AutoMapper` потребують ліцензійного ключа `LuckyPenny:LicenseKey`; без нього працює з попередженням у логу.
* Міграцію Каталогу (Лаб. 03) написано вручну, у `appsettings.json` ввімкнено `IgnorePendingModelChangesWarning`; рекомендовано
  перегенерувати міграцію через `dotnet ef`, як описано в README (Частина 3).
