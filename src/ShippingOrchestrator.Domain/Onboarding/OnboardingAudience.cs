namespace ShippingOrchestrator.Domain.Onboarding;

/// <summary>Who advances a flow's steps. Drives endpoint selection (Private vs Public API).</summary>
public enum OnboardingAudience
{
    Staff = 0,
    Tenant = 1,
    Public = 2,
}
