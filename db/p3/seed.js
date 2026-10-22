/* =====================================================================
   Проєкт №3 — тестові дані ReviewsDb. ІДЕМПОТЕНТНИЙ: фіксовані _id +
   updateOne(..., {$setOnInsert}, {upsert:true}) = find-or-create.
   Запуск ПІСЛЯ collections.js:
     mongosh "mongodb://localhost:27017" --file db/p3/seed.js
   Значення productId (1..5) збігаються з CatalogDb; customerId (1..3) та orderId —
   з OrdersDb. Це лише значення, FK між базами немає.
   ===================================================================== */
const rdb = db.getSiblingDB("ReviewsDb");
const I = NumberInt;                         // валідатор вимагає int, а не double

function seed(collName, docs) {
  let created = 0;
  docs.forEach(function (d) {
    const id = d._id;
    const rest = Object.assign({}, d);
    delete rest._id;
    const r = rdb.getCollection(collName).updateOne({ _id: id }, { $setOnInsert: rest }, { upsert: true });
    if (r.upsertedCount) created++;
  });
  print(collName + ": +" + created + " new, total " + rdb.getCollection(collName).countDocuments({}));
}

const D1 = ObjectId("66f0c3000000000000000001");
const D2 = ObjectId("66f0c3000000000000000002");
const M1 = ObjectId("66f0c4000000000000000001");

/* ---------- discussions ---------- */
seed("discussions", [
  { _id: D1, productId: I(1), productName: "Ноутбук X15", title: "Охолодження під навантаженням",
    tags: ["performance", "cooling"], messagesCount: I(3),
    lastMessageAt: ISODate("2026-09-03T12:00:00Z"), createdAt: ISODate("2026-09-01T09:00:00Z") },
  { _id: D2, productId: I(3), productName: "Навушники Air", title: "Чи підходять для спорту?",
    messagesCount: I(1), lastMessageAt: ISODate("2026-09-05T18:30:00Z"), createdAt: ISODate("2026-09-05T18:30:00Z") }
]);

/* ---------- messages (referencing; m3 — відповідь у гілці на m1) ---------- */
seed("messages", [
  { _id: M1, discussionId: D1, author: { customerId: I(2), displayName: "Андрій" },
    text: "Як поводиться ноутбук у іграх понад годину?", createdAt: ISODate("2026-09-01T09:00:00Z") },
  { _id: ObjectId("66f0c4000000000000000002"), discussionId: D1, author: { customerId: I(1), displayName: "Олена" },
    text: "У мене до 78°C, шум помірний.", createdAt: ISODate("2026-09-02T10:15:00Z") },
  { _id: ObjectId("66f0c4000000000000000003"), discussionId: D1, parentMessageId: M1,
    author: { customerId: I(3), displayName: "Ірина" },
    text: "Раджу підставку з охолодженням.", createdAt: ISODate("2026-09-03T12:00:00Z") },
  { _id: ObjectId("66f0c4000000000000000004"), discussionId: D2, author: { customerId: I(2), displayName: "Андрій" },
    text: "Тримаються добре, але для бігу замало кріплення.", createdAt: ISODate("2026-09-05T18:30:00Z") }
]);

/* ---------- reviews: документи З РІЗНИМИ наборами полів (гнучка схема) ---------- */
seed("reviews", [
  // повний відгук: photos, вбудовані comments, verifiedPurchase, reference на discussion
  { _id: ObjectId("66f0c2000000000000000001"),
    productId: I(1), productName: "Ноутбук X15",
    author: { customerId: I(1), displayName: "Олена" },
    type: "text", rating: I(5), text: "Чудовий екран",
    photos: ["img1.jpg", "img2.jpg"],
    comments: [ { author: "Андрій", text: "Згоден", createdAt: ISODate("2026-09-01T10:00:00Z") } ],
    verifiedPurchase: { orderId: I(1) },
    discussionId: D1, createdAt: ISODate("2026-08-30T08:00:00Z") },

  // відгук з pros/cons і порожніми коментарями; без photos і verifiedPurchase
  { _id: ObjectId("66f0c2000000000000000002"),
    productId: I(1), productName: "Ноутбук X15",
    author: { customerId: I(2), displayName: "Андрій" },
    type: "text", rating: I(4), text: "Швидкий, але гучний під навантаженням",
    pros: ["екран", "продуктивність"], cons: ["шум", "вага"],
    comments: [], createdAt: ISODate("2026-09-02T11:00:00Z") },

  // відео-відгук: власні поля videoUrl/durationSec, немає text
  { _id: ObjectId("66f0c2000000000000000003"),
    productId: I(3), productName: "Навушники Air",
    author: { customerId: I(1), displayName: "Олена" },
    type: "video", rating: I(5), videoUrl: "https://videos.example.com/air-review.mp4", durationSec: I(95),
    discussionId: D2, createdAt: ISODate("2026-09-04T15:00:00Z") },

  // лише оцінка
  { _id: ObjectId("66f0c2000000000000000004"),
    productId: I(2), productName: "Смартфон S10",
    author: { customerId: I(3), displayName: "Ірина" },
    type: "rating-only", rating: I(3), createdAt: ISODate("2026-09-06T09:30:00Z") }
]);

/* ---------- productRatings (проєкція; перерахунок — queries.js, п. 5) ---------- */
seed("productRatings", [
  { _id: I(1), productName: "Ноутбук X15",    count: I(2), avg: 4.5, histogram: { "1": I(0), "2": I(0), "3": I(0), "4": I(1), "5": I(1) } },
  { _id: I(2), productName: "Смартфон S10",   count: I(1), avg: 3,   histogram: { "1": I(0), "2": I(0), "3": I(1), "4": I(0), "5": I(0) } },
  { _id: I(3), productName: "Навушники Air",  count: I(1), avg: 5,   histogram: { "1": I(0), "2": I(0), "3": I(0), "4": I(0), "5": I(1) } }
]);
