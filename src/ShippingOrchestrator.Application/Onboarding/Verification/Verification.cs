using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Application.Onboarding.Verification;

public interface IVerificationProvider
{
    Task<VerificationResult> VerifyAsync(VerificationRequest request, CancellationToken cancellationToken);
}

public sealed record VerificationRequest(
    TenantId TenantId,
    string PlatformCode,
    string ExternalAccountId);

public sealed record VerificationResult(
    VerificationStatus Status,
    IReadOnlyList<string> Reasons);

public enum VerificationStatus
{
    Pass = 0,
    Fail = 1,
    NeedsReview = 2,
}

// v1 implementation: every verification passes. Real KVK/VAT/sanctions integrations drop in
// here without changes to the OAuth completion flow or aggregate transitions.
public sealed class AutoPassVerificationProvider : IVerificationProvider
{
    public Task<VerificationResult> VerifyAsync(VerificationRequest request, CancellationToken cancellationToken)
        => Task.FromResult(new VerificationResult(VerificationStatus.Pass, Array.Empty<string>()));
}

public interface IActivationPolicy
{
    ActivationDecision Decide(VerificationResult verification);
}

public sealed record ActivationDecision(bool Activate, string? RejectReason);

public sealed class DefaultActivationPolicy : IActivationPolicy
{
    public ActivationDecision Decide(VerificationResult verification) => verification.Status switch
    {
        VerificationStatus.Pass => new ActivationDecision(true, null),
        VerificationStatus.NeedsReview => new ActivationDecision(false, null),
        VerificationStatus.Fail => new ActivationDecision(false,
            verification.Reasons.Count > 0 ? string.Join("; ", verification.Reasons) : "verification failed"),
        _ => new ActivationDecision(false, "unknown verification status"),
    };
}
