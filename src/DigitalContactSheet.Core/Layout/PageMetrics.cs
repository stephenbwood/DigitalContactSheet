namespace DigitalContactSheet.Core.Layout;

/// <summary>All derived geometry for a contact sheet, in device pixels at the chosen DPI.</summary>
public sealed class PageMetrics
{
    internal PageMetrics(
        PageGeometry page,
        int columns,
        int rows,
        double marginPx,
        double spacingPx,
        double contentWidthPx,
        double contentHeightPx,
        double cellWidthPx,
        double cellHeightPx,
        double fontSizePx,
        double lineHeightPx,
        double captionGapPx,
        double captionBandPx,
        double imageAreaHeightPx,
        int imageCount)
    {
        Page = page;
        Columns = columns;
        Rows = rows;
        MarginPx = marginPx;
        SpacingPx = spacingPx;
        ContentWidthPx = contentWidthPx;
        ContentHeightPx = contentHeightPx;
        CellWidthPx = cellWidthPx;
        CellHeightPx = cellHeightPx;
        FontSizePx = fontSizePx;
        LineHeightPx = lineHeightPx;
        CaptionGapPx = captionGapPx;
        CaptionBandPx = captionBandPx;
        ImageAreaHeightPx = imageAreaHeightPx;
        ImageCount = imageCount;
        ImagesPerPage = columns * rows;
        PageCount = imageCount <= 0 ? 0 : (int)Math.Ceiling(imageCount / (double)ImagesPerPage);
    }

    public PageGeometry Page { get; }

    public int Columns { get; }

    public int Rows { get; }

    public double MarginPx { get; }

    public double SpacingPx { get; }

    public double ContentWidthPx { get; }

    public double ContentHeightPx { get; }

    public double CellWidthPx { get; }

    public double CellHeightPx { get; }

    public double FontSizePx { get; }

    public double LineHeightPx { get; }

    public double CaptionGapPx { get; }

    /// <summary>Total vertical space reserved below the image for the caption (0 when disabled).</summary>
    public double CaptionBandPx { get; }

    public double ImageAreaHeightPx { get; }

    public int ImageCount { get; }

    public int ImagesPerPage { get; }

    public int PageCount { get; }

    /// <summary>Bounding rectangle of the cell at <paramref name="slot"/> (0-based, within one page).</summary>
    public RectD GetCellRect(int slot)
    {
        var column = slot % Columns;
        var row = slot / Columns;
        var x = MarginPx + (column * (CellWidthPx + SpacingPx));
        var y = MarginPx + (row * (CellHeightPx + SpacingPx));
        return new RectD(x, y, CellWidthPx, CellHeightPx);
    }

    /// <summary>The region within a cell available to the image (excludes the caption band).</summary>
    public RectD GetImageArea(int slot)
    {
        var cell = GetCellRect(slot);
        return new RectD(cell.X, cell.Y, cell.Width, ImageAreaHeightPx);
    }

    /// <summary>The region the caption text is laid out in (below the image area).</summary>
    public RectD GetCaptionRect(int slot)
    {
        var cell = GetCellRect(slot);
        var top = cell.Y + ImageAreaHeightPx + CaptionGapPx;
        return new RectD(cell.X, top, cell.Width, LineHeightPx);
    }

    public int GetPageIndex(int imageIndex) => imageIndex / ImagesPerPage;

    public int GetSlotOnPage(int imageIndex) => imageIndex % ImagesPerPage;
}
