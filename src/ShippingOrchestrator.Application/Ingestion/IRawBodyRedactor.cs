using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace ShippingOrchestrator.Application.Ingestion;

/// <summary>
/// Strips obvious PII and credentials from a webhook body before persisting it to the
/// <c>IngestionException</c> aggregate, and produces a SHA-256 fingerprint of the original
/// bytes. The redacted excerpt is bounded so a single stuck order can't bloat the
/// orchestrator schema. The hash is the stable identity used as a lookup-key fallback
/// when <c>ExternalOrderId</c> is unparseable.
/// </summary>
public interface IRawBodyRedactor
{
    /// <summary>Redact + truncate. Output is safe to store and surface to ops.</summary>
    string Redact(string rawBody);

    /// <summary>Lowercase hex SHA-256 of the original (un-redacted) bytes.</summary>
    string Hash(byte[] rawBody);
}

public sealed class DefaultRawBodyRedactor : IRawBodyRedactor
{
    public const int MaxLength = 2048;

    private static readonly Regex EmailPattern =
        new(@"\b[\w._%+-]+@[\w.-]+\.[A-Za-z]{2,}\b", RegexOptions.Compiled);

    // Digit runs of 9+ chars with optional + prefix and common separators. Order numbers
    // get caught too, which is acceptable: ops never debug failures by phone number.
    private static readonly Regex PhonePattern =
        new(@"\+?\d(?:[\d\s().-]{7,})\d", RegexOptions.Compiled);

    private static readonly Regex SensitiveJsonKeyPattern =
        new("\"(token|secret|password|api_key|apiKey|authorization|access_token|refresh_token)\"\\s*:\\s*\"[^\"]*\"",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public string Redact(string rawBody)
    {
        if (string.IsNullOrEmpty(rawBody)) return string.Empty;

        var redacted = EmailPattern.Replace(rawBody, "[email]");
        redacted = PhonePattern.Replace(redacted, "[phone]");
        redacted = SensitiveJsonKeyPattern.Replace(redacted, m => $"\"{m.Groups[1].Value}\":\"***\"");

        return redacted.Length <= MaxLength ? redacted : redacted[..MaxLength];
    }

    public string Hash(byte[] rawBody)
    {
        ArgumentNullException.ThrowIfNull(rawBody);
        var digest = SHA256.HashData(rawBody);
        return Convert.ToHexStringLower(digest);
    }
}
