using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShippingOrchestrator.Domain.ValueObjects.JsonConverters;

public sealed class CountryCodeJsonConverter : JsonConverter<CountryCode>
{
    public override CountryCode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return value is null ? default : new CountryCode(value);
    }

    public override void Write(Utf8JsonWriter writer, CountryCode value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}
