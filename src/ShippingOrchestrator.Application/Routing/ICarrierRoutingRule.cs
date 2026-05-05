using ShippingOrchestrator.Domain.Connections;
using ShippingOrchestrator.Domain.Shipments;

namespace ShippingOrchestrator.Application.Routing;

public sealed record RoutingContext(
    Shipment Shipment,
    IReadOnlyList<CarrierAssignment> Assignments);

public sealed record ScoredCarrier(
    string CarrierCode,
    decimal Score,
    string Reason);

/// <summary>
/// One link in the routing chain. Each rule sees the candidate carriers and contributes a
/// score (or filters them out). Rules are ordered by <see cref="Priority"/> ascending and
/// the engine sums the scores. Mirrors the priority-ordered <c>EnrichmentRule</c> pattern
/// from the reference repo's <c>EnrichStep</c>.
/// </summary>
public interface ICarrierRoutingRule
{
    int Priority { get; }
    string Name { get; }
    IEnumerable<ScoredCarrier> Score(RoutingContext context, IReadOnlyDictionary<string, decimal> currentScores);
}
