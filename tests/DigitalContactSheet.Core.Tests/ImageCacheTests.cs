using DigitalContactSheet.Core.Model;
using DigitalContactSheet.Core.Rendering;
using SkiaSharp;

namespace DigitalContactSheet.Core.Tests;

/// <summary>
/// Covers the canonical-size decode cache: an image is decoded once at a generous size and smaller
/// requests are served from that bitmap, so changing the layout does not re-read the source files.
/// </summary>
public class ImageCacheTests : IDisposable
{
    private readonly string _root;

    public ImageCacheTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "dcs-cache-" + Guid.NewGuid().ToString("N"));
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

    private string CreateImage(string name, int width, int height)
    {
        var path = Path.Combine(_root, name);
        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(new SKColor(120, 90, 60));
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using (var stream = File.Create(path))
        {
            data.SaveTo(stream);
        }

        return path;
    }

    [Fact]
    public void Get_ReusesTheSameBitmapForSmallerRequests()
    {
        var path = CreateImage("small.png", 400, 300);
        var cache = new ImageCache(canonicalBox: 1024);

        var first = cache.Get(path, 200, 150, ZoomMode.Fit, out var ownedFirst);
        var second = cache.Get(path, 100, 75, ZoomMode.Fit, out var ownedSecond);

        Assert.False(ownedFirst);
        Assert.False(ownedSecond);
        Assert.Same(first, second);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void Get_DecodesAtTheCanonicalSizeNotTheRequestedSize()
    {
        // A source larger than the canonical box is decoded to fit the box, not the tiny request.
        var path = CreateImage("large.png", 2000, 1500);
        var cache = new ImageCache(canonicalBox: 1024);

        var bitmap = cache.Get(path, 100, 75, ZoomMode.Fit, out _);

        Assert.Equal(1024, bitmap.Width);
        Assert.Equal(768, bitmap.Height);
    }

    [Fact]
    public void Get_ReDecodesWhenALargerBitmapIsNeeded()
    {
        var path = CreateImage("large.png", 2000, 1500);
        var cache = new ImageCache(canonicalBox: 1024);

        // Capture the size before the entry is replaced (the cache owns and disposes the old bitmap).
        var smallWidth = cache.Get(path, 100, 75, ZoomMode.Fit, out _).Width;
        var large = cache.Get(path, 1500, 1125, ZoomMode.Fit, out _);

        Assert.Equal(1024, smallWidth);
        Assert.Equal(1500, large.Width);
        Assert.Equal(1125, large.Height);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public void Get_ReturnsABitmapLargeEnoughForBothFitAndFill()
    {
        // Fill covers the cell, so it needs more resolution than Fit for the same cell.
        var path = CreateImage("tall.png", 500, 1000);
        var cache = new ImageCache(canonicalBox: 4096);

        var fit = cache.Get(path, 200, 200, ZoomMode.Fit, out _);
        var (fitW, fitH) = ImageLoader.ComputeTargetSize(500, 1000, 200, 200, ZoomMode.Fit);
        Assert.True(fit.Width >= fitW && fit.Height >= fitH, $"fit {fit.Width}x{fit.Height} < {fitW}x{fitH}");

        var fill = cache.Get(path, 200, 200, ZoomMode.Fill, out _);
        var (fillW, fillH) = ImageLoader.ComputeTargetSize(500, 1000, 200, 200, ZoomMode.Fill);
        Assert.True(fill.Width >= fillW && fill.Height >= fillH, $"fill {fill.Width}x{fill.Height} < {fillW}x{fillH}");
    }

    [Fact]
    public void Get_EvictsWhenOverTheByteBudget()
    {
        var cache = new ImageCache(maxEntries: 100, maxBytes: 128 * 1024, canonicalBox: 128);

        for (var i = 0; i < 8; i++)
        {
            cache.Get(CreateImage($"img{i}.png", 200, 200), 64, 64, ZoomMode.Fit, out _);
        }

        Assert.True(cache.ApproximateBytes <= 128 * 1024, $"cache holds {cache.ApproximateBytes} bytes");
        Assert.True(cache.Count < 8);
    }

    [Fact]
    public void Pin_DefersDisposalUntilReleased()
    {
        var path = CreateImage("pinned.png", 200, 200);
        var cache = new ImageCache(canonicalBox: 128);

        SKBitmap bitmap;
        using (cache.Pin())
        {
            bitmap = cache.Get(path, 64, 64, ZoomMode.Fit, out _);
            cache.Clear();

            // The bitmap was evicted but must remain usable while pinned.
            Assert.Equal(1, cache.PendingDisposalCount);
            Assert.Equal(0, cache.Count);
            Assert.True(bitmap.Width > 0);
        }

        Assert.Equal(0, cache.PendingDisposalCount);
    }

    [Fact]
    public void Clear_EmptiesTheCache()
    {
        var path = CreateImage("a.png", 200, 200);
        var cache = new ImageCache(canonicalBox: 128);

        cache.Get(path, 64, 64, ZoomMode.Fit, out _);
        Assert.Equal(1, cache.Count);

        cache.Clear();

        Assert.Equal(0, cache.Count);
        Assert.Equal(0, cache.ApproximateBytes);
    }
}
