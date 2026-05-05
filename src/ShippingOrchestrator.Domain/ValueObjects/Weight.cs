namespace ShippingOrchestrator.Domain.ValueObjects;

public readonly record struct Weight(int Grams)
{
    public static Weight FromGrams(int grams)
    {
        if (grams < 0) throw new ArgumentOutOfRangeException(nameof(grams), "Weight cannot be negative.");
        return new Weight(grams);
    }

    public static Weight FromKilograms(decimal kilograms) => FromGrams((int)Math.Round(kilograms * 1000m));

    public decimal Kilograms => Grams / 1000m;

    public override string ToString() => $"{Kilograms:0.###} kg";
}
