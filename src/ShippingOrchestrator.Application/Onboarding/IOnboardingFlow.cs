using ShippingOrchestrator.Domain.Onboarding;

namespace ShippingOrchestrator.Application.Onboarding;

/// <summary>
/// Code-defined onboarding flow. Each implementation declares an ordered sequence of
/// <see cref="OnboardingStepDescriptor"/> records that drive both the server-side dispatch
/// (which command runs on advance) and the client-side rendering (which step component to
/// show). Multiple flows can coexist; a process records its <c>FlowCode</c> on creation so
/// future flow changes do not retroactively alter in-flight processes.
/// </summary>
public interface IOnboardingFlow
{
    string Code { get; }
    string DisplayTitle { get; }
    OnboardingAudience Audience { get; }
    IReadOnlyList<OnboardingStepDescriptor> Steps { get; }
}

public sealed record OnboardingStepDescriptor
{
    public required string Code { get; init; }
    public required int Sequence { get; init; }
    public required string DisplayTitle { get; init; }
    public required OnboardingStepKind Kind { get; init; }

    /// <summary>Hint the FE uses to pick a renderer (e.g. <c>tenant-form</c>, <c>oauth-redirect</c>).</summary>
    public required string RendererCode { get; init; }

    /// <summary>Type the JSON payload submitted to <c>advance</c> deserializes to (null for kinds that take no input).</summary>
    public Type? PayloadType { get; init; }

    /// <summary>Wolverine command type dispatched on advance (null for <see cref="OnboardingStepKind.AwaitExternal"/> arms).</summary>
    public Type? CommandType { get; init; }

    public bool Skippable { get; init; }

    /// <summary>True when completing this step writes a non-rewindable aggregate (tenant, connection, ...).</summary>
    public bool IsCommitted { get; init; }

    public TimeSpan? AwaitTimeout { get; init; }

    /// <summary>Optional metadata exposed to the dashboard (e.g. help text, links).</summary>
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }
}
