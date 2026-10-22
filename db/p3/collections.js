/* =====================================================================
   Проєкт №3 — ReviewsDb (MongoDB). Контекст «Відгуки та обговорення».
   Запуск: mongosh "mongodb://localhost:27017" --file db/p3/collections.js
   Ідемпотентний: колекції створюються, якщо їх немає, інакше оновлюється
   валідатор (collMod); createIndex повторно нічого не ламає.

   Моделювання:
     reviews        — EMBEDDING (author, comments[], verifiedPurchase) + REFERENCE (discussionId) = ГІБРИД
     discussions    — кореневий документ обговорення (productName — денормалізація)
     messages       — REFERENCING: необмежена кількість повідомлень -> окрема колекція (discussionId)
     productRatings — проєкція для швидкого читання (computed pattern, productName — денормалізація)
   Посилання на інші бази (productId, customerId, orderId) — без FK, лише значення.
   ===================================================================== */
const rdb = db.getSiblingDB("ReviewsDb");

function ensureCollection(name, validator) {
  const opts = { validator: validator, validationLevel: "strict", validationAction: "error" };
  if (rdb.getCollectionInfos({ name: name }).length === 0) {
    rdb.createCollection(name, opts);
    print("created: " + name);
  } else {
    rdb.runCommand(Object.assign({ collMod: name }, opts));
    print("validator updated: " + name);
  }
}

const idType = ["int", "long"];

/* ---------- reviews ---------- */
ensureCollection("reviews", { $jsonSchema: {
  bsonType: "object",
  required: ["productId", "productName", "author", "rating", "createdAt"],
  properties: {
    productId:   { bsonType: idType, minimum: 1 },                 // посилання на CatalogDb.Products.Id (без FK)
    productName: { bsonType: "string" },                           // денормалізація (оновлюється подіями)
    author: { bsonType: "object", required: ["customerId", "displayName"],
      properties: { customerId: { bsonType: idType }, displayName: { bsonType: "string" } } },
    rating: { bsonType: "int", minimum: 1, maximum: 5 },
    type:   { enum: ["text", "video", "rating-only"] },
    text:   { bsonType: "string" },
    photos: { bsonType: "array", items: { bsonType: "string" } },
    comments: { bsonType: "array", items: { bsonType: "object",
      required: ["author", "text", "createdAt"],
      properties: { author: { bsonType: "string" }, text: { bsonType: "string" }, createdAt: { bsonType: "date" } } } },
    discussionId: { bsonType: "objectId" },                        // reference на discussions
    createdAt: { bsonType: "date" }
  }
} });

/* ---------- discussions ---------- */
ensureCollection("discussions", { $jsonSchema: {
  bsonType: "object",
  required: ["productId", "productName", "title", "createdAt"],
  properties: {
    productId:   { bsonType: idType, minimum: 1 },
    productName: { bsonType: "string" },
    title:       { bsonType: "string" },
    tags:        { bsonType: "array", items: { bsonType: "string" } },
    messagesCount: { bsonType: ["int", "long"], minimum: 0 },
    lastMessageAt: { bsonType: "date" },
    createdAt:   { bsonType: "date" }
  }
} });

/* ---------- messages ---------- */
ensureCollection("messages", { $jsonSchema: {
  bsonType: "object",
  required: ["discussionId", "author", "text", "createdAt"],
  properties: {
    discussionId:    { bsonType: "objectId" },                     // reference на discussions
    parentMessageId: { bsonType: "objectId" },                     // reference на відповідь у гілці
    author: { bsonType: "object", required: ["customerId", "displayName"],
      properties: { customerId: { bsonType: idType }, displayName: { bsonType: "string" } } },
    text:      { bsonType: "string" },
    createdAt: { bsonType: "date" }
  }
} });

/* ---------- productRatings ---------- */
ensureCollection("productRatings", { $jsonSchema: {
  bsonType: "object",
  required: ["_id", "productName", "count", "avg"],
  properties: {
    _id:         { bsonType: idType },                             // _id = productId
    productName: { bsonType: "string" },
    count:       { bsonType: ["int", "long"], minimum: 0 },
    avg:         { bsonType: "number", minimum: 0, maximum: 5 },
    histogram:   { bsonType: "object" }
  }
} });

/* ---------- Індекси (кожен — під запит) ---------- */
// «відгуки товару, найновіші першими»
rdb.reviews.createIndex({ productId: 1, createdAt: -1 }, { name: "ix_reviews_product_created" });
// «один відгук користувача на товар» (унікальність)
rdb.reviews.createIndex({ productId: 1, "author.customerId": 1 }, { unique: true, name: "ux_reviews_product_author" });
// «відгуки користувача»
rdb.reviews.createIndex({ "author.customerId": 1 }, { name: "ix_reviews_author" });
// повнотекстовий пошук за текстом відгуку (українська не має вбудованого стеммера -> "none")
rdb.reviews.createIndex({ text: "text" }, { name: "tx_reviews_text", default_language: "none" });
// «обговорення товару»
rdb.discussions.createIndex({ productId: 1, lastMessageAt: -1 }, { name: "ix_discussions_product_last" });
// «повідомлення обговорення в хронологічному порядку»
rdb.messages.createIndex({ discussionId: 1, createdAt: 1 }, { name: "ix_messages_discussion_created" });
// «відповіді на повідомлення» (часткового індексу достатньо — лише документи з parentMessageId)
rdb.messages.createIndex({ parentMessageId: 1 }, { name: "ix_messages_parent", partialFilterExpression: { parentMessageId: { $exists: true } } });

print("ReviewsDb collections: " + rdb.getCollectionNames().join(", "));
