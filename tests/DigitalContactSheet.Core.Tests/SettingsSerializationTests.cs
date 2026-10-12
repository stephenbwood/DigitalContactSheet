using DigitalContactSheet.Core.Model;
using DigitalContactSheet.Core.Settings;

namespace DigitalContactSheet.Core.Tests;

public class SettingsSerializationTests
{
    [Fact]
    public void RoundTrip_PreservesAllOptions()
    {
        var settings = new AppSettings
        {
            LastSourceDirectory = @"C:\photos",
            WindowWidth = 1280,
            WindowHeight = 900,
            Options = new ContactSheetOptions
            {
                PageSizePreset = PageSizePreset.Letter,
                Orientation = Orientation.Landscape,
                Resolution = 150,
                PageMargin = Length.In(0.5),
                ImageSpacing = Length.Mm(3),
                Columns = 3,
                Rows = 4,
                BackgroundColor = new RgbaColor(16, 16, 16),
                TextColor = new RgbaColor(240, 240, 240),
                ZoomMode = ZoomMode.Fill,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                FontFamily = "Inter",
                FontWeight = FontWeightOption.Bold,
                FontSize = 12.5,
                ShowCaptions = false,
                ShowFileExtensions = false,
                CaptionGap = Length.Mm(1.5),                OutputFormat = OutputFormat.Tiff,
                JpgQuality = 75,
                OutputMode = OutputMode.IntoSourceFolder,
                OutputDirectory = @"D:\out",
                FileNamePrefix = "proofs",
                PageNumberDigits = 4,
                RecurseSubfolders = true,
                SortOrder = SortOrder.DateModified,
            },
        };

        var json = SettingsStore.Serialize(settings);
        var restored = SettingsStore.Deserialize(json);

        Assert.Equal(settings.LastSourceDirectory, restored.LastSourceDirectory);
        Assert.Equal(settings.WindowWidth, restored.WindowWidth);
        Assert.Equal(settings.WindowHeight, restored.WindowHeight);

        var a = settings.Options;
        var b = restored.Options;
        Assert.Equal(a.PageSizePreset, b.PageSizePreset);
        Assert.Equal(a.Orientation, b.Orientation);
        Assert.Equal(a.Resolution, b.Resolution);
        Assert.Equal(a.PageMargin, b.PageMargin);
        Assert.Equal(a.ImageSpacing, b.ImageSpacing);
        Assert.Equal(a.Columns, b.Columns);
        Assert.Equal(a.Rows, b.Rows);
        Assert.Equal(a.BackgroundColor, b.BackgroundColor);
        Assert.Equal(a.TextColor, b.TextColor);
        Assert.Equal(a.ZoomMode, b.ZoomMode);
        Assert.Equal(a.HorizontalAlignment, b.HorizontalAlignment);
        Assert.Equal(a.VerticalAlignment, b.VerticalAlignment);
        Assert.Equal(a.FontFamily, b.FontFamily);
        Assert.Equal(a.FontWeight, b.FontWeight);
        Assert.Equal(a.FontSize, b.FontSize);
        Assert.Equal(a.ShowCaptions, b.ShowCaptions);
        Assert.Equal(a.ShowFileExtensions, b.ShowFileExtensions);
        Assert.Equal(a.CaptionGap, b.CaptionGap);
        Assert.Equal(a.OutputFormat, b.OutputFormat);
        Assert.Equal(a.JpgQuality, b.JpgQuality);
        Assert.Equal(a.OutputMode, b.OutputMode);
        Assert.Equal(a.OutputDirectory, b.OutputDirectory);
        Assert.Equal(a.FileNamePrefix, b.FileNamePrefix);
        Assert.Equal(a.PageNumberDigits, b.PageNumberDigits);
        Assert.Equal(a.RecurseSubfolders, b.RecurseSubfolders);
        Assert.Equal(a.SortOrder, b.SortOrder);
    }

    [Fact]
    public void Deserialize_EmptyJson_ReturnsDefaults()
    {
        var settings = SettingsStore.Deserialize("{}");
        Assert.NotNull(settings.Options);
        Assert.Equal(PageSizePreset.A4, settings.Options.PageSizePreset);
    }
}

public class ColorTests
{
    [Theory]
    [InlineData("#FFFFFF", 255, 255, 255, 255)]
    [InlineData("#000", 0, 0, 0, 255)]
    [InlineData("#123456", 18, 52, 86, 255)]
    [InlineData("#12345678", 18, 52, 86, 120)]
    [InlineData("f00", 255, 0, 0, 255)]
    public void Parse_HandlesHexForms(string text, byte r, byte g, byte b, byte a)
    {
        var color = RgbaColor.Parse(text);
        Assert.Equal(new RgbaColor(r, g, b, a), color);
    }

    [Fact]
    public void ToHex_RoundTrips()
    {
        var color = new RgbaColor(18, 52, 86);
        Assert.Equal("#123456", color.ToHex());
        Assert.Equal(color, RgbaColor.Parse(color.ToHex()));
    }

    [Fact]
    public void Parse_Invalid_Throws()
    {
        Assert.Throws<FormatException>(() => RgbaColor.Parse("nope"));
    }
}
