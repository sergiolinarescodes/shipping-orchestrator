using System.Text.Json.Serialization;

namespace ShippingOrchestrator.Domain.Tenancy;

[JsonConverter(typeof(TenantIdJsonConverter))]
public readonly record struct TenantId(Guid Value)
{
    public static TenantId New() => new(Guid.NewGuid());
    public static TenantId Parse(string s) => new(Guid.Parse(s));
    public override string ToString() => Value.ToString();
}
