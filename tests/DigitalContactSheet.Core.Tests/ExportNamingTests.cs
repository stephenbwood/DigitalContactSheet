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
        var name = ExportNaming.RasterFileName("a/b:c\\d", 0, 3, OutputFormat.Jpg);
        Assert.DoesNotContain('/', name);
        Assert.DoesNotContain(':', name);
        Assert.DoesNotContain('\\', name);
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
        var root = Path.Combine(Path.GetTempPath(), "root");
        var source = Path.Combine(root, "sub", "deep");
        var output = Path.Combine(Path.GetTempPath(), "out");

        var options = new ContactSheetOptions
        {
            OutputMode = OutputMode.MirrorUnderOutputDirectory,
            OutputDirectory = output,
        };

        var destination = ExportNaming.ResolveDestination(options, root, source);
        Assert.Equal(Path.Combine(output, "sub", "deep"), destination);
    }

    [Fact]
    public void ResolveDestination_RootFolderGoesDirectlyIntoOutput()
    {
        var root = Path.Combine(Path.GetTempPath(), "root");
        var output = Path.Combine(Path.GetTempPath(), "out");

        var options = new ContactSheetOptions
        {
            OutputMode = OutputMode.MirrorUnderOutputDirectory,
            OutputDirectory = output,
        };

        var destination = ExportNaming.ResolveDestination(options, root, root);
        Assert.Equal(output, destination);
    }

    [Fact]
    public void ResolveDestination_IntoSourceFolderIgnoresOutputDir()
    {
        var root = Path.Combine(Path.GetTempPath(), "root");
        var source = Path.Combine(root, "sub");
        var output = Path.Combine(Path.GetTempPath(), "out");

        var options = new ContactSheetOptions
        {
            OutputMode = OutputMode.IntoSourceFolder,
            OutputDirectory = output,
        };

        var destination = ExportNaming.ResolveDestination(options, root, source);
        Assert.Equal(source, destination);
    }
}
