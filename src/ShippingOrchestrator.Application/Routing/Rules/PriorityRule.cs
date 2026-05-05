namespace ShippingOrchestrator.Application.Routing.Rules;

/// <summary>
/// Adds the carrier-assignment <c>Priority</c> directly to the score so tenants can
/// hand-rank their preferred carriers. Only contributes for carriers that already have
/// a positive score from <see cref="CountryAllowedRule"/> — this prevents inactive or
/// uncovered carriers from being picked just because they have a high priority.
/// </summary>
public sealed class PriorityRule : ICarrierRoutingRule
{
    public int Priority => 10;
    public string Name => "tenant-priority";

    public IEnumerable<ScoredCarrier> Score(RoutingContext context, IReadOnlyDictionary<string, decimal> currentScores)
    {
        foreach (var assignment in context.Assignments)
        {
            if (currentScores.TryGetValue(assignment.CarrierCode, out var existing) && existing > 0)
                yield return new ScoredCarrier(assignment.CarrierCode, assignment.Priority,
                    $"priority={assignment.Priority}");
        }
    }
}
