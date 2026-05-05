using ShippingOrchestrator.Application.Tenancy;
using ShippingOrchestrator.Domain.Onboarding;

namespace ShippingOrchestrator.Application.Onboarding.Flows;

/// <summary>
/// Single-step staff handoff: ops creates the tenant and copies the dashboard URL back to
/// the customer. Every connector install (Shopify, WooCommerce, future) is then driven by the
/// tenant from <c>ConnectionsPage</c> in the customer SPA — ops never touches platform OAuth.
/// Future multi-step flows (self-serve auto, email-verified, business-approved) register
/// alongside this one with the richer step set.
/// </summary>
public sealed class ManualStaffOnboardingFlow : IOnboardingFlow
{
    public const string FlowCode = "manual-staff-v1";

    public string Code => FlowCode;
    public string DisplayTitle => "Manual staff onboarding";
    public OnboardingAudience Audience => OnboardingAudience.Staff;

    public IReadOnlyList<OnboardingStepDescriptor> Steps { get; } =
    [
        new()
        {
            Code = OnboardingStepCodes.TenantCreateActive,
            Sequence = 1,
            DisplayTitle = "Create tenant",
            Kind = OnboardingStepKind.SyncInput,
            RendererCode = OnboardingRendererCodes.TenantForm,
            PayloadType = typeof(TenantCreateStepPayload),
            CommandType = typeof(CreateTenantCommand),
            IsCommitted = true,
        },
    ];
}
