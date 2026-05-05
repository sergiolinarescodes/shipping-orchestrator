namespace ShippingOrchestrator.Domain.Onboarding;

/// <summary>
/// Shape of an onboarding step. <see cref="SyncInput"/> collects user input + dispatches a
/// command immediately. <see cref="AwaitExternal"/> hands off to an external system (OAuth,
/// email verify) and resumes when the callback lands. <see cref="StaffApproval"/> waits for a
/// staff member to approve. <see cref="Automatic"/> runs without user input as soon as
/// prerequisites are satisfied (e.g. activate the tenant once everything else completed).
/// </summary>
public enum OnboardingStepKind
{
    SyncInput = 0,
    AwaitExternal = 1,
    StaffApproval = 2,
    Automatic = 3,
}
