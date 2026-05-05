namespace ShippingOrchestrator.Domain.Tenancy;

// Click-through Terms-of-Service acceptance captured when a tenant selects the master
// carrier path. Stored alongside the tenant for audit; legal copy is supplied externally.
public sealed record ToSAcceptance(
    string SignerName,
    string SignerEmail,
    string IpAddress,
    string ToSVersion,
    DateTimeOffset AcceptedAt);
