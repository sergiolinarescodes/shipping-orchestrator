namespace ShippingOrchestrator.Domain.Onboarding;

/// <summary>
/// Domain-side projection of a flow descriptor. Kept here so <see cref="OnboardingProcess"/>
/// can be hydrated from any source (code-defined flows in the Application layer, future
/// data-driven flows) without dragging the descriptor type into the Domain assembly. The
/// Application layer maps its <c>IOnboardingFlow</c> into one of these when starting a process.
/// </summary>
public sealed record OnboardingFlowBlueprint(string FlowCode, IReadOnlyList<OnboardingFlowStepBlueprint> Steps);

public sealed record OnboardingFlowStepBlueprint(
    string Code,
    int Sequence,
    bool Skippable,
    bool IsCommitted);
