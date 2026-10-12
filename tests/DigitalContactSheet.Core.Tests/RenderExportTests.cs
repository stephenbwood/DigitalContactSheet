using DigitalContactSheet.Core.Export;
using DigitalContactSheet.Core.Layout;
using DigitalContactSheet.Core.Model;
using DigitalContactSheet.Core.Rendering;
using SkiaSharp;

namespace DigitalContactSheet.Core.Tests;

public class RenderExportTests : IDisposable
{
    private readonly string _root;

    public RenderExportTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "dcs-render-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    private void CreateImage(string fileName, int width, int height, SKColor color)
    {
        var path = Path.Combine(_root, fileName);
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(color);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path);
        data.SaveTo(stream);
    }

    private void CreateImages(int count)
    {
        for (var i = 0; i < count; i++)
        {
            CreateImage($"img{i + 1}.png", 400, 300, new SKColor((byte)(40 * i), 100, 200));
        }
    }

    private ContactSheetOptions Options(OutputFormat format) => new()
    {
        OutputFormat = format,
        PageSizePreset = PageSizePreset.A4,
        Resolution = 150,
        Columns = 2,
        Rows = 2,
        PageMargin = Length.Mm(10),
        ImageSpacing = Length.Mm(4),
        FontSize = 9,
        OutputDirectory = Path.Combine(_root, "out"),
    };

    [Fact]
    public void ExportPdf_ProducesSingleMultiPageFile()
    {
        CreateImages(5); // 2x2 => 2 pages

        var options = Options(OutputFormat.Pdf);
        var plan = ExportPlanner.Build(_root, options);
        Assert.Equal(2, plan.TotalPages);

        var summary = new ExportService().Export(plan);

        Assert.Equal(1, summary.FileCount);
        var file = summary.Files.Single();
        Assert.True(File.Exists(file));
        Assert.True(new FileInfo(file).Length > 0);
        Assert.Equal(".pdf", Path.GetExtension(file));

        using var stream = File.OpenRead(file);
        var header = new byte[5];
        stream.ReadExactly(header);
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(header));
    }

    [Fact]
    public void ExportJpg_ProducesOneFilePerPage()
    {
        CreateImages(5);

        var options = Options(OutputFormat.Jpg);
        var plan = ExportPlanner.Build(_root, options);
        var summary = new ExportService().Export(plan);

        Assert.Equal(2, summary.FileCount);
        Assert.All(summary.Files, f => Assert.True(File.Exists(f)));
        Assert.Contains(summary.Files, f => f.EndsWith("contact-sheet-001.jpg"));
        Assert.Contains(summary.Files, f => f.EndsWith("contact-sheet-002.jpg"));
    }

    [Fact]
    public void ExportTiff_ProducesValidSinglePageFiles()
    {
        CreateImages(3);

        var options = Options(OutputFormat.Tiff);
        var plan = ExportPlanner.Build(_root, options);
        var summary = new ExportService().Export(plan);

        Assert.Equal(1, summary.FileCount); // 3 images in a 2x2 grid => 1 page
        var file = summary.Files.Single();
        Assert.True(File.Exists(file));
        Assert.True(new FileInfo(file).Length > 0);

        // TIFF magic number: "II" (little-endian) or "MM" (big-endian).
        var header = new byte[2];
        using (var stream = File.OpenRead(file))
        {
            stream.ReadExactly(header);
        }

        var magic = System.Text.Encoding.ASCII.GetString(header);
        Assert.True(magic is "II" or "MM", $"Unexpected TIFF header '{magic}'.");
    }

    [Fact]
    public void Export_MirrorsSubfoldersIntoOutput()
    {
        CreateImage("img1.png", 200, 200, SKColors.Red);
        var sub = Path.Combine(_root, "sub");
        Directory.CreateDirectory(sub);
        using (var bitmap = new SKBitmap(new SKImageInfo(200, 200, SKColorType.Bgra8888, SKAlphaType.Premul)))
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Blue);
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var stream = File.Create(Path.Combine(sub, "img2.png"));
            data.SaveTo(stream);
        }

        var options = Options(OutputFormat.Pdf);
        options.RecurseSubfolders = true;

        var plan = ExportPlanner.Build(_root, options);
        Assert.Equal(2, plan.Jobs.Count);

        var summary = new ExportService().Export(plan);
        Assert.Equal(2, summary.FileCount);

        var outputRoot = options.OutputDirectory;
        Assert.True(File.Exists(Path.Combine(outputRoot, "contact-sheet.pdf")));
        Assert.True(File.Exists(Path.Combine(outputRoot, "sub", "contact-sheet.pdf")));
    }

    [Fact]
    public void Preview_RendersScaledBitmapMatchingPageAspectRatio()
    {
        CreateImages(4);
        var options = Options(OutputFormat.Pdf);

        var images = Scanning.FolderScanner.GetImages(_root, SortOrder.NameNatural);
        var bitmap = PreviewRenderer.Render(options, images, pageIndex: 0, maxPixelWidth: 800, maxPixelHeight: 800);

        using (bitmap)
        {
            var metrics = LayoutEngine.Compute(options, images.Count);
            var expectedAspect = metrics.Page.PixelWidth / (double)metrics.Page.PixelHeight;
            var actualAspect = bitmap.Width / (double)bitmap.Height;

            Assert.True(bitmap.Width <= 800 && bitmap.Height <= 800);
            Assert.Equal(expectedAspect, actualAspect, 2);
        }
    }

    [Fact]
    public void RenderPage_HandlesCorruptImageWithoutThrowing()
    {
        CreateImages(1);
        File.WriteAllText(Path.Combine(_root, "broken.png"), "not an image");

        var options = Options(OutputFormat.Jpg);
        var plan = ExportPlanner.Build(_root, options);
        var summary = new ExportService().Export(plan);

        Assert.Equal(1, summary.FileCount);
    }
}
