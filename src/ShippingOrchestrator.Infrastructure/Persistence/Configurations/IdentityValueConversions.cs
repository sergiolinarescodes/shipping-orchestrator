using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ShippingOrchestrator.Domain.Identity;

namespace ShippingOrchestrator.Infrastructure.Persistence.Configurations;

internal static class IdentityValueConversions
{
    public static readonly ValueConverter<AccountId, Guid> AccountIdConverter =
        new(a => a.Value, g => new AccountId(g));

    public static readonly ValueConverter<AccountId?, Guid?> NullableAccountIdConverter =
        new(a => a == null ? null : a.Value.Value, g => g == null ? null : new AccountId(g.Value));
}
