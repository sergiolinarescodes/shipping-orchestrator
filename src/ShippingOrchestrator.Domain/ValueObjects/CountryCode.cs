using System.Text.Json.Serialization;
using ShippingOrchestrator.Domain.ValueObjects.JsonConverters;

namespace ShippingOrchestrator.Domain.ValueObjects;

/// <summary>
/// ISO-3166-1 alpha-2 country code. The wildcard "*" is allowed for carrier-coverage rules
/// meaning "any destination" — the routing engine treats it as a default-allow when no
/// explicit country match wins.
/// </summary>
[JsonConverter(typeof(CountryCodeJsonConverter))]
public readonly record struct CountryCode
{
    public const string Wildcard = "*";

    public string Value { get; }

    public CountryCode(string value) => Value = Normalize(value);

    public static CountryCode Parse(string value) => new(value);

    public bool IsWildcard => Value == Wildcard;

    public override string ToString() => Value;

    internal static string Normalize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var trimmed = value.Trim();
        if (trimmed == Wildcard) return Wildcard;
        if (trimmed.Length != 2 || !trimmed.All(char.IsLetter))
            throw new ArgumentException($"'{value}' is not a valid ISO-3166-1 alpha-2 code.", nameof(value));
        return trimmed.ToUpperInvariant();
    }
}
