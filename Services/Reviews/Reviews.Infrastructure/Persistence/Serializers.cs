using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Reviews.Domain.Enums;
using Reviews.Domain.ValueObjects;

namespace Reviews.Infrastructure.Persistence;

/// <summary>Rating (Value Object) зберігається як звичайне ціле число — як у схемі з Лаб. 01 (rating: int, 1..5).</summary>
public sealed class RatingSerializer : SerializerBase<Rating>
{
    public override Rating Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var reader = context.Reader;
        var value = reader.CurrentBsonType switch
        {
            BsonType.Int32 => reader.ReadInt32(),
            BsonType.Int64 => checked((int)reader.ReadInt64()),
            BsonType.Double => checked((int)reader.ReadDouble()),
            var t => throw new FormatException($"Cannot read Rating from BSON type {t}")
        };
        return new Rating(value);                                  // конструктор перевіряє 1..5
    }

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, Rating value)
        => context.Writer.WriteInt32(value.Value);
}

/// <summary>ReviewType зберігається рядком: text | video | rating-only.</summary>
public sealed class ReviewTypeSerializer : SerializerBase<ReviewType>
{
    public override ReviewType Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var reader = context.Reader;
        if (reader.CurrentBsonType == BsonType.Null) { reader.ReadNull(); return ReviewType.Text; }
        return ReviewTypeCodes.FromCode(reader.ReadString());
    }

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, ReviewType value)
        => context.Writer.WriteString(value.ToCode());
}
