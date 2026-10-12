using DigitalContactSheet.Core.Model;
using SkiaSharp;

namespace DigitalContactSheet.Core.Rendering;

/// <summary>Conversions between the library-neutral model types and SkiaSharp types.</summary>
public static class SkiaInterop
{
    public static SKColor ToSKColor(this RgbaColor color) => new(color.R, color.G, color.B, color.A);

    public static SKRect ToSKRect(this Layout.RectD rect) => new((float)rect.X, (float)rect.Y, (float)rect.Right, (float)rect.Bottom);
}
