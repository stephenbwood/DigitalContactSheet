namespace DigitalContactSheet.Core.Layout;

/// <summary>A simple double-precision rectangle, kept free of any rendering-library types.</summary>
public readonly record struct RectD(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;

    public double CenterX => X + (Width / 2);

    public double CenterY => Y + (Height / 2);

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public override string ToString() => $"({X:0.##}, {Y:0.##}, {Width:0.##} x {Height:0.##})";
}
