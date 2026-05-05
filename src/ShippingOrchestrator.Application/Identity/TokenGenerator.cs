using System.Security.Cryptography;

namespace ShippingOrchestrator.Application.Identity;

/// <summary>
/// Random secret + SHA-256 hashing for magic-link tokens and session ids. Both surfaces
/// follow the same shape: 32 random bytes, base64url-encoded for transport, SHA-256 of the
/// base64url string persisted in Postgres. We never store plaintext.
/// </summary>
public static class TokenGenerator
{
    public const int RawByteLength = 32;

    public static string NewRawSecret()
    {
        var bytes = new byte[RawByteLength];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    public static string Hash(string rawSecret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawSecret);
        var bytes = System.Text.Encoding.UTF8.GetBytes(rawSecret);
        var hash = SHA256.HashData(bytes);
        return Base64UrlEncode(hash);
    }

    /// <summary>Constant-time string comparison for hash verification.</summary>
    public static bool FixedTimeEquals(string left, string right)
    {
        var l = System.Text.Encoding.UTF8.GetBytes(left ?? string.Empty);
        var r = System.Text.Encoding.UTF8.GetBytes(right ?? string.Empty);
        if (l.Length != r.Length) return false;
        return CryptographicOperations.FixedTimeEquals(l, r);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
