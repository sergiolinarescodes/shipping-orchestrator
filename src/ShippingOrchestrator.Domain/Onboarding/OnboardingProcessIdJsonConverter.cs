using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShippingOrchestrator.Domain.Onboarding;

public sealed class OnboardingProcessIdJsonConverter : JsonConverter<OnboardingProcessId>
{
    public override OnboardingProcessId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetGuid());

    public override void Write(Utf8JsonWriter writer, OnboardingProcessId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}
