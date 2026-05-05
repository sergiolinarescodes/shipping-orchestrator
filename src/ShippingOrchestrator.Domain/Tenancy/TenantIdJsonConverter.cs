using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShippingOrchestrator.Domain.Tenancy;

/// <summary>
/// Round-trips <see cref="TenantId"/> as a plain Guid string instead of the default
/// record-struct nested object. Wire shape stays stable as we evolve the type.
/// </summary>
public sealed class TenantIdJsonConverter : JsonConverter<TenantId>
{
    public override TenantId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetGuid());

    public override void Write(Utf8JsonWriter writer, TenantId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}
