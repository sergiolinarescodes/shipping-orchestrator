using System.Text.Json.Serialization;

namespace ShippingOrchestrator.Domain.Onboarding;

[JsonConverter(typeof(OnboardingProcessIdJsonConverter))]
public readonly record struct OnboardingProcessId(Guid Value)
{
    public static OnboardingProcessId New() => new(Guid.NewGuid());
    public static OnboardingProcessId Parse(string s) => new(Guid.Parse(s));
    public override string ToString() => Value.ToString();
}
