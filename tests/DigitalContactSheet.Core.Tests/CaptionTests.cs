using DigitalContactSheet.Core.Model;
using DigitalContactSheet.Core.Rendering;
using DigitalContactSheet.Core.Scanning;
using SkiaSharp;

namespace DigitalContactSheet.Core.Tests;

/// <summary>Covers caption text: the file-extension toggle and left-hand truncation.</summary>
public class CaptionTests
{
    private static readonly ImageFile Image = new(
        Path.Combine(Path.GetTempPath(), "photos", "very_long_camera_file_name_sequence_000123.tif"),
        "very_long_camera_file_name_sequence_000123.tif",
        0,
        DateTime.UnixEpoch);

    private static SKFont CreateFont() => new(FontProvider.Resolve(FontProvider.BundledFamily, FontWeightOption.Regular), 12f, 1f, 0f);

    [Fact]
    public void CaptionFor_IncludesExtensionWhenEnabled()
    {
        var options = new ContactSheetOptions { ShowFileExtensions = true };
        Assert.Equal("very_long_camera_file_name_sequence_000123.tif", PageRenderer.CaptionFor(Image, options));
    }

    [Fact]
    public void CaptionFor_StripsExtensionWhenDisabled()
    {
        var options = new ContactSheetOptions { ShowFileExtensions = false };
        Assert.Equal("very_long_camera_file_name_sequence_000123", PageRenderer.CaptionFor(Image, options));
    }

    [Fact]
    public void Ellipsize_ReturnsFullTextWhenItFits()
    {
        using var font = CreateFont();
        using var paint = new SKPaint { Color = SKColors.Black };

        Assert.Equal("short.tif", PageRenderer.Ellipsize("short.tif", font, paint, 10_000));
    }

    [Fact]
    public void Ellipsize_TruncatesFromTheLeftKeepingTheSuffix()
    {
        using var font = CreateFont();
        using var paint = new SKPaint { Color = SKColors.Black };
        const string text = "very_long_camera_file_name_sequence_000123.tif";

        var result = PageRenderer.Ellipsize(text, font, paint, 60);

        Assert.StartsWith("\u2026", result);
        Assert.EndsWith(".tif", result);
        Assert.True(result.Length < text.Length);
        // The distinguishing tail is preserved, not the common prefix.
        Assert.Contains("123", result);
    }

    [Fact]
    public void Ellipsize_NeverExceedsTheAvailableWidth()
    {
        using var font = CreateFont();
        using var paint = new SKPaint { Color = SKColors.Black };
        const string text = "an_extremely_long_file_name_that_will_not_fit_anywhere.tif";

        foreach (var width in new[] { 10, 20, 40, 80, 160 })
        {
            var result = PageRenderer.Ellipsize(text, font, paint, width);
            Assert.True(font.MeasureText(result, paint) <= width, $"'{result}' exceeds {width}px");
        }
    }

    [Fact]
    public void Ellipsize_EmptyWidthReturnsEmpty()
    {
        using var font = CreateFont();
        using var paint = new SKPaint { Color = SKColors.Black };
        Assert.Equal(string.Empty, PageRenderer.Ellipsize("file.tif", font, paint, 0));
    }
}
