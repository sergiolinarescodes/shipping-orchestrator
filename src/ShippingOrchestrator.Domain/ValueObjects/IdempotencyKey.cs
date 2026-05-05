using System.Text.Json.Serialization;
using ShippingOrchestrator.Domain.ValueObjects.JsonConverters;

namespace ShippingOrchestrator.Domain.ValueObjects;

[JsonConverter(typeof(IdempotencyKeyJsonConverter))]
public readonly record struct IdempotencyKey
{
    public string Value { get; }

    public IdempotencyKey(string value) => Value = Validate(value);

    public static IdempotencyKey Parse(string value) => new(value);

    public override string ToString() => Value;

    private static string Validate(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var trimmed = value.Trim();
        if (trimmed.Length is < 8 or > 128)
            throw new ArgumentException("Idempotency key must be between 8 and 128 characters.", nameof(value));
        return trimmed;
    }
}
