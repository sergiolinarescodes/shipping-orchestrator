namespace ShippingOrchestrator.Domain.ValueObjects;

public sealed record Address(
    string Name,
    string Line1,
    string? Line2,
    string City,
    string? Region,
    string PostalCode,
    CountryCode Country,
    string? Phone = null,
    string? Email = null);
