using DigitalContactSheet.Core.Model;

namespace DigitalContactSheet.Core.Layout;

/// <summary>
/// Pure geometry for placing an image inside its cell image area. Kept separate from the
/// renderer so the fit/fill and alignment maths can be unit-tested without Skia.
/// </summary>
public static class ImagePlacement
{
    /// <summary>
    /// Computes the destination rectangle for an image of size <paramref name="imageWidth"/> x
    /// <paramref name="imageHeight"/> inside <paramref name="area"/>. For <see cref="ZoomMode.Fill"/>
    /// the returned rectangle may extend beyond the area; the caller is expected to clip to the area.
    /// </summary>
    public static RectD Compute(
        double imageWidth,
        double imageHeight,
        RectD area,
        ZoomMode mode,
        HorizontalAlignment horizontal,
        VerticalAlignment vertical)
    {
        if (imageWidth <= 0 || imageHeight <= 0 || area.IsEmpty)
        {
            return area;
        }

        var scaleX = area.Width / imageWidth;
        var scaleY = area.Height / imageHeight;
        var scale = mode == ZoomMode.Fill ? Math.Max(scaleX, scaleY) : Math.Min(scaleX, scaleY);

        var drawWidth = imageWidth * scale;
        var drawHeight = imageHeight * scale;

        var freeX = area.Width - drawWidth;
        var freeY = area.Height - drawHeight;

        double x;
        double y;

        if (mode == ZoomMode.Fill)
        {
            // Negative free space: shift by the overflow according to the alignment.
            x = area.X - HorizontalOffset(horizontal, -freeX);
            y = area.Y - VerticalOffset(vertical, -freeY);
        }
        else
        {
            x = area.X + HorizontalOffset(horizontal, freeX);
            y = area.Y + VerticalOffset(vertical, freeY);
        }

        return new RectD(x, y, drawWidth, drawHeight);
    }

    private static double HorizontalOffset(HorizontalAlignment alignment, double free) => alignment switch
    {
        HorizontalAlignment.Left => 0,
        HorizontalAlignment.Center => free / 2,
        HorizontalAlignment.Right => free,
        _ => free / 2,
    };

    private static double VerticalOffset(VerticalAlignment alignment, double free) => alignment switch
    {
        VerticalAlignment.Top => 0,
        VerticalAlignment.Middle => free / 2,
        VerticalAlignment.Bottom => free,
        _ => free / 2,
    };
}
