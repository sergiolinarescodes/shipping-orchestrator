using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShippingOrchestrator.Domain.ValueObjects.JsonConverters;

public sealed class IdempotencyKeyJsonConverter : JsonConverter<IdempotencyKey>
{
    public override IdempotencyKey Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return value is null ? default : new IdempotencyKey(value);
    }

    public override void Write(Utf8JsonWriter writer, IdempotencyKey value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}
