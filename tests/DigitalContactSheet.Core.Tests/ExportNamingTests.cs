using DigitalContactSheet.Core.Export;
using DigitalContactSheet.Core.Model;

namespace DigitalContactSheet.Core.Tests;

public class ExportNamingTests
{
    [Fact]
    public void RasterFileName_PadsPageNumbers()
    {
        Assert.Equal("sheet-001.jpg", ExportNaming.RasterFileName("sheet", 0, 3, OutputFormat.Jpg));
        Assert.Equal("sheet-042.tif", ExportNaming.RasterFileName("sheet", 41, 3, OutputFormat.Tiff));
    }

    [Fact]
    public void RasterFileName_SanitizesPrefix()
    {
        var name = ExportNaming.RasterFileName("a/b:c", 0, 3, OutputFormat.Jpg);
        Assert.DoesNotContain('/', name);
        Assert.DoesNotContain(':', name);
    }

    [Fact]
    public void PdfFileName_UsesPrefix()
    {
        Assert.Equal("contact-sheet.pdf", ExportNaming.PdfFileName("contact-sheet"));
        Assert.Equal("contact-sheet.pdf", ExportNaming.PdfFileName("  "));
    }

    [Fact]
    public void ResolveDestination_MirrorsRelativePath()
    {
        var options = new ContactSheetOptions
        {
            OutputMode = OutputMode.MirrorUnderOutputDirectory,
            OutputDirectory = @"C:\out",
        };

        var destination = ExportNaming.ResolveDestination(options, @"C:\root", @"C:\root\sub\deep");
        Assert.Equal(Path.Combine(@"C:\out", "sub", "deep"), destination);
    }

    [Fact]
    public void ResolveDestination_RootFolderGoesDirectlyIntoOutput()
    {
        var options = new ContactSheetOptions
        {
            OutputMode = OutputMode.MirrorUnderOutputDirectory,
            OutputDirectory = @"C:\out",
        };

        var destination = ExportNaming.ResolveDestination(options, @"C:\root", @"C:\root");
        Assert.Equal(@"C:\out", destination);
    }

    [Fact]
    public void ResolveDestination_IntoSourceFolderIgnoresOutputDir()
    {
        var options = new ContactSheetOptions
        {
            OutputMode = OutputMode.IntoSourceFolder,
            OutputDirectory = @"C:\out",
        };

        var destination = ExportNaming.ResolveDestination(options, @"C:\root", @"C:\root\sub");
        Assert.Equal(@"C:\root\sub", destination);
    }
}
