using ShippingOrchestrator.Domain.Onboarding;

namespace ShippingOrchestrator.Application.Onboarding;

internal sealed class OnboardingFlowRegistry(IEnumerable<IOnboardingFlow> flows) : IOnboardingFlowRegistry
{
    private readonly Dictionary<string, IOnboardingFlow> _flows = flows
        .ToDictionary(f => f.Code, StringComparer.OrdinalIgnoreCase);

    public IOnboardingFlow Resolve(string flowCode) =>
        _flows.TryGetValue(flowCode, out var flow)
            ? flow
            : throw new KeyNotFoundException($"Onboarding flow '{flowCode}' is not registered.");

    public bool TryResolve(string flowCode, out IOnboardingFlow? flow)
    {
        var ok = _flows.TryGetValue(flowCode, out var f);
        flow = f;
        return ok;
    }

    public IReadOnlyList<IOnboardingFlow> All => [.. _flows.Values];

    public OnboardingFlowBlueprint BlueprintFor(string flowCode)
    {
        var flow = Resolve(flowCode);
        return new OnboardingFlowBlueprint(
            flow.Code,
            flow.Steps
                .OrderBy(s => s.Sequence)
                .Select(s => new OnboardingFlowStepBlueprint(s.Code, s.Sequence, s.Skippable, s.IsCommitted))
                .ToArray());
    }
}
