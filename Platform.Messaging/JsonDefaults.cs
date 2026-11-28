using System.Text.Json;
using System.Text.Json.Serialization;

namespace Platform.Messaging;

/// <summary>Єдині правила серіалізації для publisher і consumer (camelCase, enum як рядки, дати ISO 8601).</summary>
public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        // обов'язкові параметри конструктора record без значення в JSON → JsonException → повідомлення йде в DLQ
        RespectRequiredProperties = true,
        Converters = { new JsonStringEnumConverter() }
    };
}
