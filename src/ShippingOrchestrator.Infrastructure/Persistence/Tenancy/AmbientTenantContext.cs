using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Infrastructure.Persistence.Tenancy;

/// <summary>
/// Scoped <see cref="ITenantContext"/> backed by <see cref="AsyncLocal{T}"/> so the tenant
/// identity propagates across async hops within a single request scope. Set by middleware
/// in the API hosts (after JWT validation) and by the saga-context middleware in the
/// Worker host before each handler runs.
/// </summary>
public sealed class AmbientTenantContext : ITenantContext
{
    private static readonly AsyncLocal<TenantId?> _current = new();

    public TenantId? Current => _current.Value;

    public static IDisposable Set(TenantId tenantId)
    {
        var previous = _current.Value;
        _current.Value = tenantId;
        return new Scope(previous);
    }

    private sealed class Scope(TenantId? previous) : IDisposable
    {
        public void Dispose() => _current.Value = previous;
    }
}
