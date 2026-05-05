using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Application.Onboarding.Flows;

/// <summary>Single tenant-create step payload. Future multi-step flows declare their own.</summary>
public sealed record TenantCreateStepPayload(
    string DisplayName,
    string? ContactEmail,
    TenantCarrierMode? CarrierMode = null,
    ToSAcceptance? ToSAcceptance = null);
