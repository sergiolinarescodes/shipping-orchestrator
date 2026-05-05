namespace ShippingOrchestrator.Domain.Onboarding;

public enum OnboardingStepStatus
{
    Pending = 0,
    Awaiting = 1,
    Completed = 2,
    Skipped = 3,
    Failed = 4,
}
