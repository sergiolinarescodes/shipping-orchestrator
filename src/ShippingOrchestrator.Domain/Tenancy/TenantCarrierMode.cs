namespace ShippingOrchestrator.Domain.Tenancy;

// Whether a tenant ships under our master carrier account (click-through ToS) or under
// their own carrier contract (BYO credentials).
public enum TenantCarrierMode
{
    Master = 0,
    Byo = 1,
}
