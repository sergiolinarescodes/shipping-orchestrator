using ShippingOrchestrator.Application.Common.Repositories;
using ShippingOrchestrator.Domain.Shipments;

namespace ShippingOrchestrator.Application.Routing;

public sealed record RoutingDecision(
    string CarrierCode,
    decimal TotalScore,
    IReadOnlyList<string> AppliedRuleAttributions);

public sealed class RoutingEngine
{
    private readonly IReadOnlyList<ICarrierRoutingRule> _rules;
    private readonly ICarrierAssignmentRepository _assignments;

    public RoutingEngine(IEnumerable<ICarrierRoutingRule> rules, ICarrierAssignmentRepository assignments)
    {
        _rules = rules.OrderBy(r => r.Priority).ToArray();
        _assignments = assignments;
    }

    public async Task<RoutingDecision?> SelectCarrierAsync(Shipment shipment, CancellationToken ct)
    {
        var assignments = await _assignments.ListForTenantAsync(shipment.TenantId, ct).ConfigureAwait(false);
        if (assignments.Count == 0) return null;

        var context = new RoutingContext(shipment, assignments);
        var scores = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var attributions = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var rule in _rules)
        {
            foreach (var scored in rule.Score(context, scores))
            {
                scores.TryGetValue(scored.CarrierCode, out var current);
                scores[scored.CarrierCode] = current + scored.Score;
                if (!attributions.TryGetValue(scored.CarrierCode, out var list))
                    attributions[scored.CarrierCode] = list = [];
                list.Add($"{rule.Name}:{scored.Score:0.##} ({scored.Reason})");
            }
        }

        if (scores.Count == 0) return null;
        var winner = scores.OrderByDescending(kv => kv.Value).First();
        return new RoutingDecision(winner.Key, winner.Value, attributions[winner.Key]);
    }
}
