using System.Text.Json;
using ShippingOrchestrator.Domain.Onboarding;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Application.Onboarding;

/// <summary>
/// Per-step server-side execution. The dispatcher knows how to (a) bind a JSON payload to the
/// descriptor's <c>PayloadType</c>, (b) build the matching Wolverine command from the payload
/// + process state (e.g. injecting <see cref="TenantId"/> into a per-tenant command), and
/// (c) translate the command result into the JSON snapshot stored on the step record.
/// Implemented in Application; composed via DI so each command type maps to a dispatcher.
/// </summary>
public interface IOnboardingStepDispatcher
{
    Task<OnboardingStepInvocationResult> DispatchAsync(
        OnboardingProcess process,
        OnboardingStepDescriptor descriptor,
        JsonElement? payload,
        CancellationToken cancellationToken);
}

public sealed record OnboardingStepInvocationResult(
    OnboardingStepInvocationOutcome Outcome,
    JsonDocument? CollectedPayload,
    JsonDocument? ResultPayload,
    TenantId? BoundTenantId,
    string? AwaitCorrelationId,
    DateTimeOffset? AwaitExpiresAt,
    string? FailureReason);

public enum OnboardingStepInvocationOutcome
{
    Completed,
    Awaiting,
    Failed,
}
