using DigitalContactSheet.Core.Layout;
using DigitalContactSheet.Core.Model;

namespace DigitalContactSheet.Core.Tests;

public class ImagePlacementTests
{
    private static readonly RectD Area = new(0, 0, 200, 200);

    [Fact]
    public void Fit_ScalesDownToFitAndCentres()
    {
        var rect = ImagePlacement.Compute(100, 50, Area, ZoomMode.Fit, HorizontalAlignment.Center, VerticalAlignment.Middle);

        Assert.Equal(200, rect.Width, 3);
        Assert.Equal(100, rect.Height, 3);
        Assert.Equal(0, rect.X, 3);
        Assert.Equal(50, rect.Y, 3);
    }

    [Fact]
    public void Fit_AlignsTopLeft()
    {
        var rect = ImagePlacement.Compute(100, 50, Area, ZoomMode.Fit, HorizontalAlignment.Left, VerticalAlignment.Top);

        Assert.Equal(0, rect.X, 3);
        Assert.Equal(0, rect.Y, 3);
    }

    [Fact]
    public void Fit_AlignsBottomRight()
    {
        var rect = ImagePlacement.Compute(100, 50, Area, ZoomMode.Fit, HorizontalAlignment.Right, VerticalAlignment.Bottom);

        Assert.Equal(0, rect.X, 3);
        Assert.Equal(100, rect.Y, 3);
    }

    [Fact]
    public void Fill_CoversAreaAndCentres()
    {
        var rect = ImagePlacement.Compute(100, 50, Area, ZoomMode.Fill, HorizontalAlignment.Center, VerticalAlignment.Middle);

        Assert.Equal(400, rect.Width, 3);
        Assert.Equal(200, rect.Height, 3);
        Assert.Equal(-100, rect.X, 3);
        Assert.Equal(0, rect.Y, 3);
    }

    [Fact]
    public void Fill_AlignsRightKeepsRightEdge()
    {
        var rect = ImagePlacement.Compute(100, 50, Area, ZoomMode.Fill, HorizontalAlignment.Right, VerticalAlignment.Top);

        Assert.Equal(-200, rect.X, 3);
        Assert.Equal(0, rect.Y, 3);
        Assert.Equal(Area.Right, rect.Right, 3);
    }

    [Fact]
    public void Fill_AlignsBottomKeepsBottomEdge()
    {
        var rect = ImagePlacement.Compute(50, 100, Area, ZoomMode.Fill, HorizontalAlignment.Left, VerticalAlignment.Bottom);

        Assert.Equal(200, rect.Width, 3);
        Assert.Equal(400, rect.Height, 3);
        Assert.Equal(Area.Bottom, rect.Y + rect.Height, 3);
    }
}
