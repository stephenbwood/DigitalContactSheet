using DigitalContactSheet.Core.Model;

namespace DigitalContactSheet.Core.Layout;

/// <summary>Derived page dimensions in device pixels plus their PDF-point equivalents.</summary>
public readonly record struct PageGeometry(
    double WidthPoints,
    double HeightPoints,
    int PixelWidth,
    int PixelHeight,
    double Dpi)
{
    public static PageGeometry From(ContactSheetOptions options)
    {
        var size = options.ResolvePageSize();
        var dpi = options.Resolution;
        var widthPoints = size.WidthInches * 72.0;
        var heightPoints = size.HeightInches * 72.0;
        var pixelWidth = (int)Math.Round(size.WidthInches * dpi);
        var pixelHeight = (int)Math.Round(size.HeightInches * dpi);
        return new PageGeometry(widthPoints, heightPoints, pixelWidth, pixelHeight, dpi);
    }
}
