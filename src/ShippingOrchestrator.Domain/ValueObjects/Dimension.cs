namespace ShippingOrchestrator.Domain.ValueObjects;

public readonly record struct Dimension(int LengthMm, int WidthMm, int HeightMm)
{
    public static Dimension FromCentimeters(int lengthCm, int widthCm, int heightCm) =>
        new(lengthCm * 10, widthCm * 10, heightCm * 10);

    public int VolumeCubicMm => LengthMm * WidthMm * HeightMm;

    public override string ToString() => $"{LengthMm}x{WidthMm}x{HeightMm} mm";
}
