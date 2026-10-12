using DigitalContactSheet.Core.Layout;
using DigitalContactSheet.Core.Model;

namespace DigitalContactSheet.Core.Tests;

public class LayoutEngineTests
{
    private static ContactSheetOptions Defaults() => new()
    {
        PageSizePreset = PageSizePreset.A4,
        Orientation = Orientation.Portrait,
        Resolution = 300,
        PageMargin = Length.Mm(10),
        ImageSpacing = Length.Mm(5),
        Columns = 4,
        Rows = 5,
        FontSize = 10,
        ShowCaptions = true,
        CaptionGap = Length.Mm(2),
    };

    [Fact]
    public void Compute_ProducesExpectedGeometry()
    {
        var metrics = LayoutEngine.Compute(Defaults(), imageCount: 20);

        Assert.Equal(2480, metrics.Page.PixelWidth);
        Assert.Equal(3508, metrics.Page.PixelHeight);
        Assert.Equal(20, metrics.ImagesPerPage);
        Assert.Equal(1, metrics.PageCount);

        Assert.Equal(118.11, metrics.MarginPx, 2);
        Assert.Equal(59.06, metrics.SpacingPx, 2);
        Assert.Equal(516.65, metrics.CellWidthPx, 2);
        Assert.Equal(607.11, metrics.CellHeightPx, 2);
        Assert.Equal(41.67, metrics.FontSizePx, 2);
        Assert.Equal(71.54, metrics.CaptionBandPx, 2);
        Assert.Equal(535.57, metrics.ImageAreaHeightPx, 2);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(20, 1)]
    [InlineData(21, 2)]
    [InlineData(40, 2)]
    [InlineData(41, 3)]
    public void Compute_Paginates(int imageCount, int expectedPages)
    {
        var metrics = LayoutEngine.Compute(Defaults(), imageCount);
        Assert.Equal(expectedPages, metrics.PageCount);
    }

    [Fact]
    public void GetCellRect_FirstCellSitsAtMargin()
    {
        var metrics = LayoutEngine.Compute(Defaults(), 20);
        var cell = metrics.GetCellRect(0);

        Assert.Equal(metrics.MarginPx, cell.X, 3);
        Assert.Equal(metrics.MarginPx, cell.Y, 3);
        Assert.Equal(metrics.CellWidthPx, cell.Width, 3);
        Assert.Equal(metrics.CellHeightPx, cell.Height, 3);
    }

    [Fact]
    public void GetCellRect_WrapsByColumnCount()
    {
        var metrics = LayoutEngine.Compute(Defaults(), 20);

        var first = metrics.GetCellRect(0);
        var second = metrics.GetCellRect(1);
        var secondRow = metrics.GetCellRect(metrics.Columns);

        Assert.Equal(first.X + metrics.CellWidthPx + metrics.SpacingPx, second.X, 3);
        Assert.Equal(first.Y, second.Y, 3);
        Assert.Equal(first.X, secondRow.X, 3);
        Assert.Equal(first.Y + metrics.CellHeightPx + metrics.SpacingPx, secondRow.Y, 3);
    }

    [Fact]
    public void GetImageArea_ExcludesCaptionBand()
    {
        var metrics = LayoutEngine.Compute(Defaults(), 20);
        var area = metrics.GetImageArea(0);
        var cell = metrics.GetCellRect(0);

        Assert.Equal(metrics.ImageAreaHeightPx, area.Height, 3);
        Assert.Equal(cell.Height - metrics.CaptionBandPx, area.Height, 3);
    }

    [Fact]
    public void CaptionsDisabled_ImageAreaEqualsCell()
    {
        var options = Defaults();
        options.ShowCaptions = false;

        var metrics = LayoutEngine.Compute(options, 20);

        Assert.Equal(0, metrics.CaptionBandPx);
        Assert.Equal(metrics.CellHeightPx, metrics.ImageAreaHeightPx, 3);
    }

    [Fact]
    public void Compute_ThrowsWhenMarginTooLarge()
    {
        var options = Defaults();
        options.PageMargin = Length.Mm(500);

        Assert.Throws<LayoutException>(() => LayoutEngine.Compute(options, 1));
    }

    [Fact]
    public void Compute_ThrowsWhenSpacingTooLarge()
    {
        var options = Defaults();
        options.ImageSpacing = Length.Mm(1000);

        Assert.Throws<LayoutException>(() => LayoutEngine.Compute(options, 1));
    }

    [Fact]
    public void Landscape_SwapsPageDimensions()
    {
        var portrait = LayoutEngine.Compute(Defaults(), 1);
        var options = Defaults();
        options.Orientation = Orientation.Landscape;
        var landscape = LayoutEngine.Compute(options, 1);

        Assert.Equal(portrait.Page.PixelWidth, landscape.Page.PixelHeight);
        Assert.Equal(portrait.Page.PixelHeight, landscape.Page.PixelWidth);
    }
}
