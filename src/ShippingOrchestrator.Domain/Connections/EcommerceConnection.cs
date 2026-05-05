using ShippingOrchestrator.Domain.Abstractions;
using ShippingOrchestrator.Domain.Events;
using ShippingOrchestrator.Domain.Tenancy;

namespace ShippingOrchestrator.Domain.Connections;

/// <summary>
/// Per-tenant link to an external ecommerce platform (Shopify shop, WooCommerce site,
/// custom REST endpoint). OAuth tokens or API keys are stored as ciphertext —
/// the encryption itself is handled by the Infrastructure layer's KMS envelope.
/// </summary>
public sealed class EcommerceConnection : AggregateRoot
{
    public Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public string PlatformCode { get; private set; } = string.Empty;
    public string ExternalAccountId { get; private set; } = string.Empty;
    public byte[] CredentialsCipher { get; private set; } = [];
    public EcommerceConnectionStatus Status { get; private set; }
    public DateTimeOffset InstalledAt { get; private set; }
    public DateTimeOffset? LastSyncAt { get; private set; }
    public DateTimeOffset? VerifiedAt { get; private set; }
    public DateTimeOffset? RejectedAt { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTimeOffset? DisconnectedAt { get; private set; }
    public string? DisconnectReason { get; private set; }

    private EcommerceConnection() { }

    public static EcommerceConnection Install(
        TenantId tenantId,
        string platformCode,
        string externalAccountId,
        byte[] credentialsCipher,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(platformCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalAccountId);
        ArgumentNullException.ThrowIfNull(credentialsCipher);

        var connection = new EcommerceConnection
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PlatformCode = platformCode.ToLowerInvariant(),
            ExternalAccountId = externalAccountId,
            CredentialsCipher = credentialsCipher,
            Status = EcommerceConnectionStatus.PendingVerification,
            InstalledAt = now,
        };
        connection.Raise(new EcommerceConnectionInstalled(
            connection.Id, tenantId, connection.PlatformCode, externalAccountId, now));
        return connection;
    }

    public void RecordSync(DateTimeOffset now) => LastSyncAt = now;

    public void RotateCredentials(byte[] newCipher, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(newCipher);
        CredentialsCipher = newCipher;
        Raise(new EcommerceConnectionCredentialsRotated(Id, TenantId, now));
    }

    public void MarkVerified(DateTimeOffset now)
    {
        if (Status == EcommerceConnectionStatus.Active) return;
        if (Status == EcommerceConnectionStatus.Rejected)
            throw new InvalidOperationException("Cannot verify a rejected connection.");
        Status = EcommerceConnectionStatus.Active;
        VerifiedAt = now;
        Raise(new EcommerceConnectionVerified(Id, TenantId, now));
    }

    public void MarkRejected(string reason, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        Status = EcommerceConnectionStatus.Rejected;
        RejectedAt = now;
        RejectionReason = reason;
        Raise(new EcommerceConnectionRejected(Id, TenantId, reason, now));
    }

}
