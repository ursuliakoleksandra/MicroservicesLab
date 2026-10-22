/* Запити й агрегації ReviewsDb (після seed.js):
   mongosh "mongodb://localhost:27017" --file db/p3/queries.js */
const rdb = db.getSiblingDB("ReviewsDb");

print("\n1. Документи з різними наборами полів (гнучка схема):");
printjson(rdb.reviews.find({}, { type: 1, photos: 1, pros: 1, videoUrl: 1, verifiedPurchase: 1 }).toArray());

print("\n2. Відгуки товару 1, найновіші першими (індекс ix_reviews_product_created):");
printjson(rdb.reviews.find({ productId: 1 }).sort({ createdAt: -1 }).toArray());

print("\n3. Середній рейтинг і кількість відгуків за товаром:");
printjson(rdb.reviews.aggregate([
  { $group: { _id: { productId: "$productId", productName: "$productName" }, avg: { $avg: "$rating" }, count: { $sum: 1 } } },
  { $sort: { avg: -1 } }
]).toArray());

print("\n4. Кількість вбудованих коментарів у відгуку (поле може бути відсутнє):");
printjson(rdb.reviews.aggregate([
  { $project: { productName: 1, rating: 1, commentsCount: { $size: { $ifNull: ["$comments", []] } } } }
]).toArray());

print("\n5. $lookup: відгук + обговорення за reference discussionId:");
printjson(rdb.reviews.aggregate([
  { $match: { discussionId: { $exists: true } } },
  { $lookup: { from: "discussions", localField: "discussionId", foreignField: "_id", as: "discussion" } },
  { $project: { productName: 1, rating: 1, "discussion.title": 1, "discussion.messagesCount": 1 } }
]).toArray());

print("\n6. Повідомлення обговорення з гілками (reference + parentMessageId):");
printjson(rdb.messages.find({ discussionId: ObjectId("66f0c3000000000000000001") }).sort({ createdAt: 1 }).toArray());

print("\n7. Повнотекстовий пошук:");
printjson(rdb.reviews.find({ $text: { $search: "екран" } }, { productName: 1, text: 1 }).toArray());

print("\n8. Перерахунок проєкції productRatings ($merge):");
rdb.reviews.aggregate([
  { $group: { _id: "$productId", productName: { $first: "$productName" }, count: { $sum: 1 }, avg: { $avg: "$rating" },
              r1: { $sum: { $cond: [{ $eq: ["$rating", 1] }, 1, 0] } }, r2: { $sum: { $cond: [{ $eq: ["$rating", 2] }, 1, 0] } },
              r3: { $sum: { $cond: [{ $eq: ["$rating", 3] }, 1, 0] } }, r4: { $sum: { $cond: [{ $eq: ["$rating", 4] }, 1, 0] } },
              r5: { $sum: { $cond: [{ $eq: ["$rating", 5] }, 1, 0] } } } },
  { $project: { productName: 1, count: 1, avg: 1, histogram: { "1": "$r1", "2": "$r2", "3": "$r3", "4": "$r4", "5": "$r5" } } },
  { $merge: { into: "productRatings", whenMatched: "replace", whenNotMatched: "insert" } }
]);
printjson(rdb.productRatings.find().toArray());

print("\n9. Як оновлюється денормалізоване поле за подією ProductRenamed з Каталогу (приклад, закоментовано):");
print('   db.reviews.updateMany({ productId: 1 }, { $set: { productName: "<нова назва>" } })');
