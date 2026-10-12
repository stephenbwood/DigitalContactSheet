namespace DigitalContactSheet.Core.Model;

/// <summary>
/// A length expressed in a specific <see cref="Unit"/>, convertible to pixels at a given DPI
/// or to PDF points.
/// </summary>
public readonly record struct Length(double Value, Unit Unit)
{
    public static Length Mm(double value) => new(value, Unit.Millimeters);

    public static Length In(double value) => new(value, Unit.Inches);

    public static Length Pt(double value) => new(value, Unit.Points);

    /// <summary>Value converted to inches.</summary>
    public double ToInches() => Unit switch
    {
        Unit.Millimeters => Value / 25.4,
        Unit.Inches => Value,
        Unit.Points => Value / 72.0,
        _ => Value,
    };

    /// <summary>Value converted to PDF points (1/72 inch).</summary>
    public double ToPoints() => ToInches() * 72.0;

    /// <summary>Value converted to device pixels at the supplied resolution.</summary>
    public double ToPixels(double dpi) => ToInches() * dpi;

    public override string ToString() => Unit switch
    {
        Unit.Millimeters => $"{Value:0.##} mm",
        Unit.Inches => $"{Value:0.###} in",
        Unit.Points => $"{Value:0.##} pt",
        _ => Value.ToString("0.##"),
    };
}
