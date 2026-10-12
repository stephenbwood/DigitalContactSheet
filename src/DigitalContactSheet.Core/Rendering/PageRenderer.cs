using DigitalContactSheet.Core.Layout;
using DigitalContactSheet.Core.Model;
using DigitalContactSheet.Core.Scanning;
using SkiaSharp;

namespace DigitalContactSheet.Core.Rendering;

/// <summary>
/// Draws a single contact-sheet page onto an <see cref="SKCanvas"/>. The same code path is used
/// for the on-screen preview, raster export (JPG/TIFF) and vector export (PDF); callers are
/// responsible for scaling the canvas (PDF is drawn in points, rasters in device pixels).
/// </summary>
public sealed class PageRenderer
{
    private static readonly SKSamplingOptions Sampling = new(SKFilterMode.Linear, SKMipmapMode.Linear);

    private readonly ImageCache? _cache;

    /// <param name="cache">
    /// Optional decoded-image cache. The live preview supplies one; exports pass none so bitmaps are
    /// released as soon as each page is written.
    /// </param>
    public PageRenderer(ImageCache? cache = null)
    {
        _cache = cache;
    }

    /// <summary>Renders page <paramref name="pageIndex"/> of <paramref name="images"/>.</summary>
    public void RenderPage(
        SKCanvas canvas,
        PageMetrics metrics,
        IReadOnlyList<ImageFile> images,
        int pageIndex,
        ContactSheetOptions options)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(metrics);
        ArgumentNullException.ThrowIfNull(images);
        ArgumentNullException.ThrowIfNull(options);

        // Keep cached bitmaps alive for the duration of the render even if the cache evicts them.
        using var pin = _cache?.Pin();

        canvas.Clear(options.BackgroundColor.ToSKColor());

        using var imagePaint = new SKPaint { IsAntialias = true };
        using var textPaint = new SKPaint { Color = options.TextColor.ToSKColor(), IsAntialias = true };

        var typeface = FontProvider.Resolve(options.FontFamily, options.FontWeight);
        using var font = new SKFont(typeface, (float)metrics.FontSizePx, 1f, 0f)
        {
            Subpixel = true,
            Edging = SKFontEdging.SubpixelAntialias,
        };

        var perPage = metrics.ImagesPerPage;
        var startIndex = pageIndex * perPage;
        var slotCount = Math.Min(perPage, images.Count - startIndex);
        if (slotCount <= 0)
        {
            return;
        }

        // Decoding is by far the most expensive step, so decode the page's images in parallel and
        // then draw them sequentially (SKCanvas is not thread-safe).
        var bitmaps = new SKBitmap?[slotCount];
        var owned = new bool[slotCount];
        Parallel.For(0, slotCount, slot =>
        {
            var area = metrics.GetImageArea(slot);
            try
            {
                if (_cache is not null)
                {
                    bitmaps[slot] = _cache.Get(images[startIndex + slot].Path, area.Width, area.Height, options.ZoomMode, out owned[slot]);
                }
                else
                {
                    bitmaps[slot] = ImageLoader.Decode(images[startIndex + slot].Path, area.Width, area.Height, options.ZoomMode);
                    owned[slot] = true;
                }
            }
            catch (ImageLoadException)
            {
                // A corrupt or unsupported file leaves the cell blank rather than aborting the page.
                bitmaps[slot] = null;
            }
        });

        try
        {
            for (var slot = 0; slot < slotCount; slot++)
            {
                var image = images[startIndex + slot];
                if (bitmaps[slot] is { } bitmap)
                {
                    DrawImage(canvas, metrics, slot, bitmap, options, imagePaint);
                }

                if (options.ShowCaptions)
                {
                    DrawCaption(canvas, metrics, slot, image, options, font, textPaint);
                }
            }
        }
        finally
        {
            for (var slot = 0; slot < slotCount; slot++)
            {
                if (owned[slot])
                {
                    bitmaps[slot]?.Dispose();
                }
            }
        }
    }

    /// <summary>Renders a page into a new bitmap sized to the page's pixel dimensions.</summary>
    public SKBitmap RenderToBitmap(
        PageMetrics metrics,
        IReadOnlyList<ImageFile> images,
        int pageIndex,
        ContactSheetOptions options)
    {
        var info = new SKImageInfo(
            metrics.Page.PixelWidth,
            metrics.Page.PixelHeight,
            SKColorType.Bgra8888,
            SKAlphaType.Premul);

        var bitmap = new SKBitmap(info);
        using (var canvas = new SKCanvas(bitmap))
        {
            RenderPage(canvas, metrics, images, pageIndex, options);
            canvas.Flush();
        }

        return bitmap;
    }

    /// <summary>Renders a page into an open PDF document as a new page.</summary>
    public void RenderPageToDocument(
        SKDocument document,
        PageMetrics metrics,
        IReadOnlyList<ImageFile> images,
        int pageIndex,
        ContactSheetOptions options)
    {
        var width = (float)metrics.Page.WidthPoints;
        var height = (float)metrics.Page.HeightPoints;
        var canvas = document.BeginPage(width, height);

        // Layout is expressed in device pixels at the chosen DPI; PDF works in points.
        var scale = (float)(72.0 / metrics.Page.Dpi);
        canvas.Scale(scale, scale);
        RenderPage(canvas, metrics, images, pageIndex, options);
        canvas.Flush();

        document.EndPage();
    }

    private static void DrawImage(
        SKCanvas canvas,
        PageMetrics metrics,
        int slot,
        SKBitmap bitmap,
        ContactSheetOptions options,
        SKPaint paint)
    {
        var area = metrics.GetImageArea(slot);

        var destination = ImagePlacement.Compute(
            bitmap.Width,
            bitmap.Height,
            area,
            options.ZoomMode,
            options.HorizontalAlignment,
            options.VerticalAlignment).ToSKRect();

        if (options.ZoomMode == ZoomMode.Fill)
        {
            canvas.Save();
            canvas.ClipRect(area.ToSKRect(), SKClipOperation.Intersect, true);
            canvas.DrawBitmap(bitmap, destination, Sampling, paint);
            canvas.Restore();
        }
        else
        {
            canvas.DrawBitmap(bitmap, destination, Sampling, paint);
        }
    }

    private static void DrawCaption(
        SKCanvas canvas,
        PageMetrics metrics,
        int slot,
        ImageFile image,
        ContactSheetOptions options,
        SKFont font,
        SKPaint paint)
    {
        var rect = metrics.GetCaptionRect(slot);
        var text = Ellipsize(CaptionFor(image, options), font, paint, rect.Width);
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var textWidth = font.MeasureText(text, paint);
        var x = options.HorizontalAlignment switch
        {
            HorizontalAlignment.Left => rect.X,
            HorizontalAlignment.Right => rect.Right - textWidth,
            _ => rect.CenterX - (textWidth / 2.0),
        };

        var fontMetrics = font.Metrics;
        var baseline = rect.CenterY - ((fontMetrics.Ascent + fontMetrics.Descent) / 2.0);

        canvas.DrawText(text, (float)x, (float)baseline, SKTextAlign.Left, font, paint);
    }

    /// <summary>The caption text for an image, honouring the "show file extension" setting.</summary>
    internal static string CaptionFor(ImageFile image, ContactSheetOptions options) =>
        options.ShowFileExtensions ? image.FileName : Path.GetFileNameWithoutExtension(image.FileName);

    /// <summary>
    /// Shortens <paramref name="text"/> with a leading ellipsis so it fits within
    /// <paramref name="maxWidth"/>. File names are truncated from the left because the tail — the
    /// sequence number and extension — is what distinguishes one file from another.
    /// </summary>
    internal static string Ellipsize(string text, SKFont font, SKPaint paint, double maxWidth)
    {
        if (maxWidth <= 0)
        {
            return string.Empty;
        }

        if (font.MeasureText(text, paint) <= maxWidth)
        {
            return text;
        }

        const string ellipsis = "\u2026";
        var low = 0;
        var high = text.Length;
        var best = string.Empty;

        // Keep the longest suffix that still fits: "…" + text[^mid..]. If not even the ellipsis
        // fits, nothing is drawn.
        while (low <= high)
        {
            var mid = (low + high) / 2;
            var candidate = ellipsis + text[^mid..].TrimStart();
            if (font.MeasureText(candidate, paint) <= maxWidth)
            {
                best = candidate;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return best;
    }
}
