using ShippingOrchestrator.Domain.Abstractions;
using ShippingOrchestrator.Domain.Events;

namespace ShippingOrchestrator.Domain.Tenancy;

public sealed class Tenant : AggregateRoot
{
    public TenantId Id { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public TenantStatus Status { get; private set; }
    public string? ContactEmail { get; private set; }
    public TenantCarrierMode? CarrierMode { get; private set; }
    public ToSAcceptance? ToSAcceptance { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Tenant() { }

    public static Tenant Create(
        string displayName,
        string? contactEmail,
        DateTimeOffset now,
        TenantCarrierMode? carrierMode = null,
        ToSAcceptance? tosAcceptance = null,
        bool activateImmediately = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        var initialStatus = activateImmediately ? TenantStatus.Active : TenantStatus.Onboarding;
        var tenant = new Tenant
        {
            Id = TenantId.New(),
            DisplayName = displayName.Trim(),
            Status = initialStatus,
            ContactEmail = contactEmail?.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
        };
        tenant.Raise(new TenantCreated(tenant.Id, tenant.DisplayName, tenant.Status, now));
        if (carrierMode is not null)
            tenant.SelectCarrierMode(carrierMode.Value, tosAcceptance, now);
        return tenant;
    }

    public void Activate(DateTimeOffset now)
    {
        if (Status == TenantStatus.Active) return;
        Status = TenantStatus.Active;
        UpdatedAt = now;
        Raise(new TenantStatusChanged(Id, Status, now));
    }

    public void Suspend(DateTimeOffset now, string reason)
    {
        Status = TenantStatus.Suspended;
        UpdatedAt = now;
        Raise(new TenantStatusChanged(Id, Status, now, reason));
    }

    public void SelectCarrierMode(TenantCarrierMode mode, ToSAcceptance? tos, DateTimeOffset now)
    {
        if (mode == TenantCarrierMode.Master && tos is null)
            throw new InvalidOperationException("Master carrier mode requires Terms-of-Service acceptance.");
        CarrierMode = mode;
        ToSAcceptance = tos;
        UpdatedAt = now;
        Raise(new TenantCarrierModeSelected(Id, mode, tos, now));
    }
}
