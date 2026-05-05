namespace ShippingOrchestrator.Application.Onboarding.Flows;

/// <summary>
/// Stable string codes used to identify steps across the wire (FE renderer registry,
/// telemetry, persistence). Treat these as part of the public contract once a flow ships.
/// </summary>
public static class OnboardingStepCodes
{
    /// <summary>Generic tenant create — leaves the tenant in <c>Onboarding</c> for multi-step flows.</summary>
    public const string TenantCreate = "tenant.create";

    /// <summary>
    /// Tenant-only ops handoff: creates the tenant directly in <c>Active</c> status so the
    /// customer can log in and self-serve every connector install from the dashboard.
    /// </summary>
    public const string TenantCreateActive = "tenant.create.active";
}

public static class OnboardingRendererCodes
{
    public const string TenantForm = "tenant-form";
}
