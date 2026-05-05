using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ShippingOrchestrator.Domain.Tenancy;
using ShippingOrchestrator.Domain.ValueObjects;

namespace ShippingOrchestrator.Infrastructure.Persistence.Configurations;

internal static class ValueConversions
{
    public static readonly ValueConverter<TenantId, Guid> TenantIdConverter =
        new(t => t.Value, g => new TenantId(g));

    public static readonly ValueConverter<TenantId?, Guid?> NullableTenantIdConverter =
        new(t => t == null ? null : t.Value.Value, g => g == null ? null : new TenantId(g.Value));

    public static readonly ValueConverter<IdempotencyKey, string> IdempotencyKeyConverter =
        new(k => k.Value, s => IdempotencyKey.Parse(s));

    public static readonly ValueConverter<IdempotencyKey?, string?> NullableIdempotencyKeyConverter =
        new(k => k == null ? null : k.Value.Value, s => s == null ? null : IdempotencyKey.Parse(s));

    public static readonly ValueConverter<CountryCode, string> CountryCodeConverter =
        new(c => c.Value, s => CountryCode.Parse(s));
}
