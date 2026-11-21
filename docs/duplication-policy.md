# Політика дублювання даних і узгодженості

Принцип: у кожного дубльованого поля є **єдиний власник**. Копії лише читаються й ніколи не редагуються незалежно (жодного dual write). Між базами немає FOREIGN KEY.

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
| `Products.AvailableStock` (CatalogDb) | CatalogDb | Каталог (фізичний залишок на складі) | Не дублюється в інших базах; не плутати з `OrdersDb.Products.Stock` (резерв для замовлень, власник — Замовлення) |
