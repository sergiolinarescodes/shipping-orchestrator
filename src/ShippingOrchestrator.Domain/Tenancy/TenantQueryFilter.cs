namespace ShippingOrchestrator.Domain.Tenancy;

/// <summary>
/// EF-friendly facade over <see cref="ITenantContext"/> used by global tenant query filters.
/// EF's parameter extractor evaluates BOTH sides of the filter's <c>||</c> expression at
/// query-setup, so a direct <c>_tenant.Current.Value.Value</c> access would throw
/// <see cref="NullReferenceException"/> whenever a worker/system path runs with no tenant.
/// This wrapper exposes a bypass flag plus a safe-default Guid so EF can extract the
/// parameters without ever dereferencing a missing nullable.
///
/// Lives in Domain so both <c>Infrastructure</c> (write context) and <c>ReadModels</c>
/// (customer + ops contexts) can depend on it without crossing the layer wall.
/// </summary>
public sealed class TenantQueryFilter(ITenantContext tenantContext)
{
    private readonly ITenantContext _tenantContext = tenantContext;

    public bool IsAnonymous => _tenantContext.Current is null;

    public Guid RequiredTenantGuid => _tenantContext.Current is { } id ? id.Value : Guid.Empty;

    public TenantId RequiredTenant => _tenantContext.Current ?? default;

    /// <summary>
    /// Filter that disables tenant scoping. Lets EF design-time tools and integration tests
    /// build a DbContext without the full DI graph; runtime hosts always inject a real one.
    /// </summary>
    public static readonly TenantQueryFilter Anonymous = new(new AnonymousTenantContext());

    private sealed class AnonymousTenantContext : ITenantContext
    {
        public TenantId? Current => null;
    }
}
