namespace ShippingOrchestrator.Application.Routing.Rules;

/// <summary>
/// Hard filter: a carrier is eligible only if its assignment covers the shipment's
/// origin and destination countries. Eligible carriers receive a base score of 100 so
/// downstream tie-breakers operate from a positive floor.
/// </summary>
public sealed class CountryAllowedRule : ICarrierRoutingRule
{
    public int Priority => 0;
    public string Name => "country-allowed";

    public IEnumerable<ScoredCarrier> Score(RoutingContext context, IReadOnlyDictionary<string, decimal> currentScores)
    {
        var origin = context.Shipment.From.Country;
        var destination = context.Shipment.To.Country;

        foreach (var assignment in context.Assignments)
        {
            if (assignment.Covers(origin, destination))
                yield return new ScoredCarrier(assignment.CarrierCode, 100m,
                    $"covers {origin}->{destination}");
        }
    }
}
