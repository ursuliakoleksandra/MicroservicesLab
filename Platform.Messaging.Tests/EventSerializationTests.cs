using System.Text.Json;
using Platform.Messaging.Events;

namespace Platform.Messaging.Tests;

public class EventSerializationTests
{
    [Fact]
    public void PriceChangedEvent_RoundTrips_ThroughJson()
    {
        var original = new ProductPriceChangedEvent(7, "Ноутбук X15", 100m, 120.5m) { CorrelationId = "corr-1" };

        var bytes = JsonSerializer.SerializeToUtf8Bytes(original, JsonDefaults.Options);
        var copy = JsonSerializer.Deserialize<ProductPriceChangedEvent>(bytes, JsonDefaults.Options);

        Assert.Equal(original, copy);
        Assert.Equal("corr-1", copy!.CorrelationId);
    }

    [Fact]
    public void Json_UsesCamelCase()
    {
        var json = JsonSerializer.Serialize(new ProductRenamedEvent(1, "A", "B"), JsonDefaults.Options);

        Assert.Contains("\"productId\":1", json);
        Assert.Contains("\"newName\":\"B\"", json);
        Assert.Contains("\"eventId\"", json);
        Assert.Contains("\"correlationId\"", json);
    }

    [Fact]
    public void Events_AreImmutable_WithExpression_ProducesCopy()
    {
        var a = new ProductRenamedEvent(1, "A", "B");
        var b = a with { NewName = "C" };

        Assert.Equal("B", a.NewName);
        Assert.Equal("C", b.NewName);
        Assert.Equal(a.EventId, b.EventId);
    }

    [Fact]
    public void EachEvent_GetsUniqueEventId()
    {
        Assert.NotEqual(new ProductRenamedEvent(1, "A", "B").EventId, new ProductRenamedEvent(1, "A", "B").EventId);
    }

    [Fact]
    public void MissingRequiredFields_FailDeserialization()
    {
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<ProductPriceChangedEvent>("{}"u8, JsonDefaults.Options));
    }

    [Fact]
    public void InvalidJson_FailsDeserialization()
    {
        Assert.ThrowsAny<JsonException>(() =>
            JsonSerializer.Deserialize<ProductPriceChangedEvent>("{ not json"u8, JsonDefaults.Options));
    }
}
