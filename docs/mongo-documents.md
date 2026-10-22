# Приклади документів ReviewsDb (MongoDB)

Усі документи — з `db/p3/seed.js`. Типи: `NumberInt` показано як числа, `ObjectId`/`ISODate` — розширений JSON.

## reviews — embedding + reference (гібрид), різна структура документів

```json
// 1) повний відгук: вбудовані author, comments[], photos[], verifiedPurchase + reference на discussions
{
  "_id": ObjectId("66f0c2000000000000000001"),
  "productId": 1,                              // посилання на CatalogDb (без FK)
  "productName": "Ноутбук X15",                // денормалізація (оновлюється подіями)
  "author": { "customerId": 1, "displayName": "Олена" },   // вбудований об'єкт (знімок імені)
  "type": "text", "rating": 5, "text": "Чудовий екран",
  "photos": ["img1.jpg", "img2.jpg"],          // поле є лише в частини документів
  "comments": [ { "author": "Андрій", "text": "Згоден", "createdAt": ISODate("2026-09-01T10:00:00Z") } ],
  "verifiedPurchase": { "orderId": 1 },        // посилання на OrdersDb.Orders (без FK)
  "discussionId": ObjectId("66f0c3000000000000000001"),    // reference
  "createdAt": ISODate("2026-08-30T08:00:00Z")
}

// 2) інший набір полів: pros/cons, порожні comments, без photos
{ "_id": ObjectId("66f0c2000000000000000002"), "productId": 1, "productName": "Ноутбук X15",
  "author": { "customerId": 2, "displayName": "Андрій" }, "type": "text", "rating": 4,
  "text": "Швидкий, але гучний під навантаженням",
  "pros": ["екран", "продуктивність"], "cons": ["шум", "вага"], "comments": [], "createdAt": ISODate("2026-09-02T11:00:00Z") }

// 3) відео-відгук: videoUrl, durationSec; немає text
{ "_id": ObjectId("66f0c2000000000000000003"), "productId": 3, "productName": "Навушники Air",
  "author": { "customerId": 1, "displayName": "Олена" }, "type": "video", "rating": 5,
  "videoUrl": "https://videos.example.com/air-review.mp4", "durationSec": 95,
  "discussionId": ObjectId("66f0c3000000000000000002"), "createdAt": ISODate("2026-09-04T15:00:00Z") }

// 4) лише оцінка
{ "_id": ObjectId("66f0c2000000000000000004"), "productId": 2, "productName": "Смартфон S10",
  "author": { "customerId": 3, "displayName": "Ірина" }, "type": "rating-only", "rating": 3, "createdAt": ISODate("2026-09-06T09:30:00Z") }
```

## discussions + messages — referencing (необмежена підколекція)

```json
{ "_id": ObjectId("66f0c3000000000000000001"), "productId": 1, "productName": "Ноутбук X15",
  "title": "Охолодження під навантаженням", "tags": ["performance", "cooling"],
  "messagesCount": 3, "lastMessageAt": ISODate("2026-09-03T12:00:00Z"), "createdAt": ISODate("2026-09-01T09:00:00Z") }

{ "_id": ObjectId("66f0c4000000000000000003"), "discussionId": ObjectId("66f0c3000000000000000001"),
  "parentMessageId": ObjectId("66f0c4000000000000000001"),     // відповідь у гілці
  "author": { "customerId": 3, "displayName": "Ірина" }, "text": "Раджу підставку з охолодженням.",
  "createdAt": ISODate("2026-09-03T12:00:00Z") }
```

**Чому так:** коментарі до відгуку — мало і завжди читаються разом із ним → *embed*. Повідомлень в обговоренні можуть бути тисячі → окрема колекція `messages` + *reference* (`discussionId`). Відгук посилається на обговорення — *гібрид*.

## productRatings — проєкція (computed pattern)

```json
{ "_id": 1, "productName": "Ноутбук X15", "count": 2, "avg": 4.5, "histogram": { "1": 0, "2": 0, "3": 0, "4": 1, "5": 1 } }
```
