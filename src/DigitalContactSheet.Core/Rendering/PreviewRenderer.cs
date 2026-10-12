using DigitalContactSheet.Core.Layout;
using DigitalContactSheet.Core.Model;
using DigitalContactSheet.Core.Scanning;
using SkiaSharp;

namespace DigitalContactSheet.Core.Rendering;

/// <summary>
/// Renders a single page to a bitmap scaled down to fit a preview area. The page is re-laid-out at
/// a proportionally lower DPI so the result looks identical to the exported output, just smaller.
/// </summary>
public static class PreviewRenderer
{
    public static SKBitmap Render(
        ContactSheetOptions options,
        IReadOnlyList<ImageFile> images,
        int pageIndex,
        int maxPixelWidth,
        int maxPixelHeight,
        ImageCache? cache = null)
    {
        var metrics = LayoutEngine.Compute(options, images.Count);

        var scaleX = maxPixelWidth / (double)metrics.Page.PixelWidth;
        var scaleY = maxPixelHeight / (double)metrics.Page.PixelHeight;
        var scale = Math.Min(1.0, Math.Min(scaleX, scaleY));
        if (scale <= 0 || double.IsNaN(scale))
        {
            scale = 1.0;
        }

        var previewOptions = options.Clone();
        previewOptions.Resolution = Math.Max(1, (int)Math.Round(options.Resolution * scale));

        var previewMetrics = LayoutEngine.Compute(previewOptions, images.Count);
        return new PageRenderer(cache).RenderToBitmap(previewMetrics, images, pageIndex, previewOptions);
    }
}
