namespace ShippingOrchestrator.Domain.Identity;

public readonly record struct AccountId(Guid Value)
{
    public static AccountId New() => new(Guid.NewGuid());
    public static AccountId Parse(string s) => new(Guid.Parse(s));
    public override string ToString() => Value.ToString();
}
