using System.Text.Json;
using ShippingOrchestrator.Domain.Onboarding;

namespace ShippingOrchestrator.Application.Onboarding;

/// <summary>
/// Wire shape returned by <c>GET /admin/onboarding/{id}</c>. Combines the in-flight aggregate
/// with the descriptor metadata so the UI can render generically without duplicating the
/// flow's shape on the client. Computed by <see cref="OnboardingProcessViewProjector"/>.
/// </summary>
/// <remarks>
/// Enum-shaped fields are projected to their string names (e.g. <c>"InProgress"</c>) instead
/// of the underlying integer so the JSON shape stays stable and the FE can declare them as
/// string literal unions. The relevant hosts don't register a global
/// <c>JsonStringEnumConverter</c>, and we don't want to add one just for this view.
/// </remarks>
public sealed record OnboardingProcessView(
    Guid ProcessId,
    string FlowCode,
    string FlowTitle,
    string Status,
    Guid? TenantId,
    string? StartedByStaffUserId,
    string? ContactEmail,
    string? CurrentStepCode,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<OnboardingStepView> Steps,
    string? DashboardUrl = null);

public sealed record OnboardingStepView(
    string Code,
    int Sequence,
    string DisplayTitle,
    string Kind,
    string RendererCode,
    string Status,
    bool Skippable,
    bool IsCommitted,
    string? FailureReason,
    string? ExternalCorrelationId,
    DateTimeOffset? AwaitingExpiresAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    JsonElement? CollectedPayload,
    JsonElement? ResultPayload,
    IReadOnlyDictionary<string, string>? Metadata);

public sealed record OnboardingFlowSummary(
    string Code,
    string DisplayTitle,
    string Audience,
    IReadOnlyList<OnboardingFlowStepSummary> Steps);

public sealed record OnboardingFlowStepSummary(
    string Code,
    int Sequence,
    string DisplayTitle,
    string Kind,
    string RendererCode);

public static class OnboardingProcessViewProjector
{
    public static OnboardingProcessView Project(OnboardingProcess process, IOnboardingFlow flow)
    {
        var byCode = flow.Steps.ToDictionary(s => s.Code, StringComparer.OrdinalIgnoreCase);
        var current = process.Steps
            .OrderBy(s => s.Sequence)
            .FirstOrDefault(s => s.Status is OnboardingStepStatus.Pending or OnboardingStepStatus.Awaiting or OnboardingStepStatus.Failed);

        return new OnboardingProcessView(
            process.Id.Value,
            flow.Code,
            flow.DisplayTitle,
            process.Status.ToString(),
            process.TenantId?.Value,
            process.StartedByStaffUserId,
            process.ContactEmail,
            current?.Code,
            process.CreatedAt,
            process.UpdatedAt,
            process.CompletedAt,
            process.Steps
                .OrderBy(s => s.Sequence)
                .Select(s =>
                {
                    var d = byCode[s.Code];
                    return new OnboardingStepView(
                        s.Code,
                        s.Sequence,
                        d.DisplayTitle,
                        d.Kind.ToString(),
                        d.RendererCode,
                        s.Status.ToString(),
                        d.Skippable,
                        d.IsCommitted,
                        s.FailureReason,
                        s.ExternalCorrelationId,
                        s.AwaitingExpiresAt,
                        s.StartedAt,
                        s.CompletedAt,
                        s.CollectedPayload is null ? null : JsonDocument.Parse(s.CollectedPayload.RootElement.GetRawText()).RootElement,
                        s.ResultPayload is null ? null : JsonDocument.Parse(s.ResultPayload.RootElement.GetRawText()).RootElement,
                        d.Metadata);
                })
                .ToArray());
    }

    public static OnboardingFlowSummary Summary(IOnboardingFlow flow) => new(
        flow.Code,
        flow.DisplayTitle,
        flow.Audience.ToString(),
        flow.Steps
            .OrderBy(s => s.Sequence)
            .Select(s => new OnboardingFlowStepSummary(s.Code, s.Sequence, s.DisplayTitle, s.Kind.ToString(), s.RendererCode))
            .ToArray());
}
