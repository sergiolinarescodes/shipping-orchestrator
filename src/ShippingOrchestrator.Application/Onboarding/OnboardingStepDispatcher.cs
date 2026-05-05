using System.Text.Json;
using System.Text.Json.Serialization;
using FluentValidation;
using ShippingOrchestrator.Application.Onboarding.Flows;
using ShippingOrchestrator.Application.Tenancy;
using ShippingOrchestrator.Domain.Onboarding;
using Wolverine;

namespace ShippingOrchestrator.Application.Onboarding;

/// <summary>
/// Maps a step <c>Code</c> to a command-builder + result-folder. Tenant-only flow today, with
/// room for future flows to add cases without touching the AdvanceOnboardingStepHandler.
/// </summary>
internal sealed class OnboardingStepDispatcher(IMessageBus bus) : IOnboardingStepDispatcher
{
    private static readonly JsonSerializerOptions JsonOptions = BuildJsonOptions();

    private static JsonSerializerOptions BuildJsonOptions()
    {
        var opts = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        opts.Converters.Add(new JsonStringEnumConverter());
        return opts;
    }

    public async Task<OnboardingStepInvocationResult> DispatchAsync(
        OnboardingProcess process,
        OnboardingStepDescriptor descriptor,
        JsonElement? payload,
        CancellationToken cancellationToken)
    {
        try
        {
            return descriptor.Code switch
            {
                OnboardingStepCodes.TenantCreate => await DispatchTenantCreate(payload, activateImmediately: false, cancellationToken).ConfigureAwait(false),
                OnboardingStepCodes.TenantCreateActive => await DispatchTenantCreate(payload, activateImmediately: true, cancellationToken).ConfigureAwait(false),
                _ => Failed($"Step '{descriptor.Code}' has no dispatcher mapping."),
            };
        }
        catch (ValidationException ex)
        {
            return Failed(string.Join("; ", ex.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}")));
        }
        catch (Exception ex)
        {
            return Failed(ex.Message);
        }
    }

    private async Task<OnboardingStepInvocationResult> DispatchTenantCreate(
        JsonElement? payload, bool activateImmediately, CancellationToken ct)
    {
        var input = Bind<TenantCreateStepPayload>(payload);
        var result = await bus.InvokeAsync<CreateTenantResult>(
                new CreateTenantCommand(input.DisplayName, input.ContactEmail, input.CarrierMode, input.ToSAcceptance, activateImmediately), ct)
            .ConfigureAwait(false);
        return Completed(
            collected: input,
            result: new TenantCreateStepResult(result.TenantId.Value, result.Status.ToString()),
            tenant: result.TenantId);
    }

    private static T Bind<T>(JsonElement? payload) where T : class
    {
        if (payload is null || payload.Value.ValueKind == JsonValueKind.Null || payload.Value.ValueKind == JsonValueKind.Undefined)
            throw new ValidationException($"Step requires a payload of type {typeof(T).Name}.");
        var bound = JsonSerializer.Deserialize<T>(payload.Value.GetRawText(), JsonOptions)
            ?? throw new ValidationException($"Could not deserialize payload to {typeof(T).Name}.");
        return bound;
    }

    private static OnboardingStepInvocationResult Completed(object? collected, object? result, Domain.Tenancy.TenantId? tenant) =>
        new(
            OnboardingStepInvocationOutcome.Completed,
            collected is null ? null : JsonSerializer.SerializeToDocument(collected, JsonOptions),
            result is null ? null : JsonSerializer.SerializeToDocument(result, JsonOptions),
            tenant,
            AwaitCorrelationId: null,
            AwaitExpiresAt: null,
            FailureReason: null);

    private static OnboardingStepInvocationResult Failed(string reason) =>
        new(
            OnboardingStepInvocationOutcome.Failed,
            CollectedPayload: null,
            ResultPayload: null,
            BoundTenantId: null,
            AwaitCorrelationId: null,
            AwaitExpiresAt: null,
            FailureReason: reason);
}

public sealed record TenantCreateStepResult(Guid TenantId, string Status);
