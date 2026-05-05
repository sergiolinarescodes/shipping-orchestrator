namespace ShippingOrchestrator.Domain.ValueObjects;

public sealed record Parcel(
    Weight Weight,
    Dimension Dimensions,
    Money DeclaredValue,
    string? Reference = null,
    string? Description = null);
