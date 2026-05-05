using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Modules.Abstractions.Ecommerce;

public sealed record OAuthInstallRequest(
    TenantId TenantId,
    string ExternalAccountId,
    string RedirectUri,
    string State);

public sealed record OAuthInstallUrl(string AuthorizationUrl);

public sealed record OAuthCallback(
    TenantId TenantId,
    string ExternalAccountId,
    string Code,
    string State,
    IReadOnlyDictionary<string, string> AdditionalParameters);

public sealed record OAuthInstallResult(
    bool Success,
    string? ExternalAccountId,
    byte[]? CredentialsPayload,
    string? FailureReason = null);

public sealed record RawWebhook(
    string EventType,
    IReadOnlyDictionary<string, string> Headers,
    string Body);

public sealed record WebhookHandled(bool Accepted, string? Reason = null);

public sealed record FulfillmentUpdate(
    string ExternalOrderId,
    string CarrierCode,
    string TrackingNumber,
    string? LabelUri);
