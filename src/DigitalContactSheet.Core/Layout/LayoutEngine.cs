using DigitalContactSheet.Core.Model;

namespace DigitalContactSheet.Core.Layout;

/// <summary>Raised when the supplied options cannot produce a valid layout.</summary>
public sealed class LayoutException(string message) : Exception(message);

/// <summary>Computes <see cref="PageMetrics"/> from a set of options and an image count.</summary>
public static class LayoutEngine
{
    /// <summary>Multiplier applied to the font size to obtain the caption line height.</summary>
    public const double LineSpacingFactor = 1.15;

    public static PageMetrics Compute(ContactSheetOptions options, int imageCount)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Columns < 1)
        {
            throw new LayoutException("Column count must be at least 1.");
        }

        if (options.Rows < 1)
        {
            throw new LayoutException("Row count must be at least 1.");
        }

        if (options.Resolution < 1)
        {
            throw new LayoutException("Resolution must be at least 1 DPI.");
        }

        var page = PageGeometry.From(options);
        var dpi = page.Dpi;

        var marginPx = options.PageMargin.ToPixels(dpi);
        var spacingPx = options.ImageSpacing.ToPixels(dpi);

        var contentWidthPx = page.PixelWidth - (2 * marginPx);
        var contentHeightPx = page.PixelHeight - (2 * marginPx);

        if (contentWidthPx <= 0 || contentHeightPx <= 0)
        {
            throw new LayoutException("Page margin is too large for the selected page size.");
        }

        var cellWidthPx = (contentWidthPx - ((options.Columns - 1) * spacingPx)) / options.Columns;
        var cellHeightPx = (contentHeightPx - ((options.Rows - 1) * spacingPx)) / options.Rows;

        if (cellWidthPx <= 0 || cellHeightPx <= 0)
        {
            throw new LayoutException("Too many rows/columns (or spacing too large) for the selected page size.");
        }

        var fontSizePx = options.FontSize * dpi / 72.0;
        if (options.FontSize <= 0)
        {
            throw new LayoutException("Font size must be greater than zero.");
        }

        var lineHeightPx = fontSizePx * LineSpacingFactor;
        var captionGapPx = options.ShowCaptions ? options.CaptionGap.ToPixels(dpi) : 0;
        var captionBandPx = options.ShowCaptions ? lineHeightPx + captionGapPx : 0;

        if (captionBandPx >= cellHeightPx)
        {
            throw new LayoutException("Caption band is taller than the cell; reduce the font size or row count.");
        }

        var imageAreaHeightPx = cellHeightPx - captionBandPx;

        return new PageMetrics(
            page,
            options.Columns,
            options.Rows,
            marginPx,
            spacingPx,
            contentWidthPx,
            contentHeightPx,
            cellWidthPx,
            cellHeightPx,
            fontSizePx,
            lineHeightPx,
            captionGapPx,
            captionBandPx,
            imageAreaHeightPx,
            imageCount);
    }
}
