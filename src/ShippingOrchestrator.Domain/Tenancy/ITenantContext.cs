namespace ShippingOrchestrator.Domain.Tenancy;

/// <summary>
/// Ambient tenant identity for the current scope. Public-API hosts populate this from the
/// tenant JWT claim before any handler runs; the Worker host populates it from the saga
/// context. Persistence-layer code consults <see cref="Current"/> to set the Postgres
/// session variable that drives Row-Level Security.
/// </summary>
public interface ITenantContext
{
    TenantId? Current { get; }
}
