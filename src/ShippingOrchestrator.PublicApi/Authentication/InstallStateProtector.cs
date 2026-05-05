using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace ShippingOrchestrator.PublicApi.Authentication;

/// <summary>
/// Stateless protector for the OAuth/key-exchange "state" token we hand to platforms during
/// dashboard-driven installs. The SPA calls
/// <c>POST /v1/dashboard/connections/{platform}/install</c>; we mint a token that captures
/// (tenantId, platformCode, externalAccountId) plus an expiry, sign it with ASP.NET's
/// data-protection key ring, and embed it in the install URL. When the platform calls back
/// (Shopify <c>state=token</c> or WooCommerce <c>user_id=token</c>) we round-trip it through
/// the same protector to recover the tenant — without consulting any server-side store.
/// Tampering, expiry, or cross-tenant replay all fail validation. The protector key ring is
/// per-host; a token issued by one host won't validate on another. Acceptable for dev / single
/// PublicApi deployments; for multi-host prod, configure a shared persisted keyring.
/// </summary>
public sealed class InstallStateProtector
{
    private const string Purpose = "ShippingOrchestrator.ConnectionInstallState.v1";
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(15);
    private readonly ITimeLimitedDataProtector _protector;

    public InstallStateProtector(IDataProtectionProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _protector = provider.CreateProtector(Purpose).ToTimeLimitedDataProtector();
    }

    public string Protect(InstallStatePayload payload, TimeSpan? ttl = null)
    {
        var json = JsonSerializer.Serialize(payload);
        return _protector.Protect(json, ttl ?? DefaultTtl);
    }

    public bool TryUnprotect(string token, out InstallStatePayload? payload)
    {
        payload = null;
        if (string.IsNullOrWhiteSpace(token)) return false;
        try
        {
            var json = _protector.Unprotect(token);
            payload = JsonSerializer.Deserialize<InstallStatePayload>(json);
            return payload is not null;
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            // Bad signature, expired token, or wrong purpose — caller treats as auth failure.
            return false;
        }
    }
}

public sealed record InstallStatePayload(
    Guid TenantId,
    string PlatformCode,
    string ExternalAccountId);
