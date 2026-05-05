namespace ShippingOrchestrator.Domain.ValueObjects;

/// <summary>
/// Canonical service level. Carriers map their proprietary service codes to one of these
/// in their anti-corruption layer so the domain stays carrier-agnostic.
/// </summary>
public readonly record struct ServiceLevel(string Code)
{
    public static readonly ServiceLevel Standard = new("STANDARD");
    public static readonly ServiceLevel Express = new("EXPRESS");
    public static readonly ServiceLevel Overnight = new("OVERNIGHT");
    public static readonly ServiceLevel Economy = new("ECONOMY");

    public override string ToString() => Code;
}
