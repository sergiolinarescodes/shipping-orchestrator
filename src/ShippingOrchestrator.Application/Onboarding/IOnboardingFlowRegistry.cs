using ShippingOrchestrator.Domain.Onboarding;

namespace ShippingOrchestrator.Application.Onboarding;

public interface IOnboardingFlowRegistry
{
    IOnboardingFlow Resolve(string flowCode);
    bool TryResolve(string flowCode, out IOnboardingFlow? flow);
    IReadOnlyList<IOnboardingFlow> All { get; }

    /// <summary>Helper that builds the Domain-side blueprint for a flow.</summary>
    OnboardingFlowBlueprint BlueprintFor(string flowCode);
}
