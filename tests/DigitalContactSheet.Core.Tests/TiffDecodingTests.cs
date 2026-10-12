using BitMiracle.LibTiff.Classic;
using DigitalContactSheet.Core.Model;
using DigitalContactSheet.Core.Rendering;
using DigitalContactSheet.Core.Scanning;
using SkiaSharp;
using TiffOrientation = BitMiracle.LibTiff.Classic.Orientation;

namespace DigitalContactSheet.Core.Tests;

/// <summary>
/// Regression tests for TIFF decoding. SkiaSharp has no TIFF codec, so <see cref="ImageLoader"/>
/// delegates to <see cref="TiffDecoder"/>. These cover the bit depths that broke in the field
/// (16-bit greyscale and 48-bit RGB) as well as orientation and down-sampling.
/// </summary>
public class TiffDecodingTests : IDisposable
{
    private readonly string _root;

    public TiffDecodingTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "dcs-tiff-" + Guid.NewGuid().ToString("N"));
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

    private string TestPath(string name) => System.IO.Path.Combine(_root, name);

    // ---- Fixture writers -------------------------------------------------

    private void WriteGray16(string name, ushort[] values, int width, int height, TiffOrientation orientation = TiffOrientation.TOPLEFT)
    {
        using var tiff = Tiff.Open(TestPath(name), "w")!;
        tiff.SetField(TiffTag.IMAGEWIDTH, width);
        tiff.SetField(TiffTag.IMAGELENGTH, height);
        tiff.SetField(TiffTag.SAMPLESPERPIXEL, 1);
        tiff.SetField(TiffTag.BITSPERSAMPLE, 16);
        tiff.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);
        tiff.SetField(TiffTag.PHOTOMETRIC, Photometric.MINISBLACK);
        tiff.SetField(TiffTag.ORIENTATION, orientation);
        tiff.SetField(TiffTag.ROWSPERSTRIP, height);

        var row = new byte[width * 2];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var v = values[(y * width) + x];
                row[x * 2] = (byte)(v & 0xFF);
                row[(x * 2) + 1] = (byte)(v >> 8);
            }

            tiff.WriteScanline(row, y);
        }

        tiff.WriteDirectory();
    }

    private void WriteRgb16(string name, (ushort R, ushort G, ushort B)[] pixels, int width, int height, TiffOrientation orientation = TiffOrientation.TOPLEFT)
    {
        using var tiff = Tiff.Open(TestPath(name), "w")!;
        tiff.SetField(TiffTag.IMAGEWIDTH, width);
        tiff.SetField(TiffTag.IMAGELENGTH, height);
        tiff.SetField(TiffTag.SAMPLESPERPIXEL, 3);
        tiff.SetField(TiffTag.BITSPERSAMPLE, 16);
        tiff.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);
        tiff.SetField(TiffTag.PHOTOMETRIC, Photometric.RGB);
        tiff.SetField(TiffTag.ORIENTATION, orientation);
        tiff.SetField(TiffTag.ROWSPERSTRIP, height);

        var row = new byte[width * 6];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var (r, g, b) = pixels[(y * width) + x];
                var o = x * 6;
                row[o] = (byte)(r & 0xFF); row[o + 1] = (byte)(r >> 8);
                row[o + 2] = (byte)(g & 0xFF); row[o + 3] = (byte)(g >> 8);
                row[o + 4] = (byte)(b & 0xFF); row[o + 5] = (byte)(b >> 8);
            }

            tiff.WriteScanline(row, y);
        }

        tiff.WriteDirectory();
    }

    private void WriteRgb8(string name, (byte R, byte G, byte B)[] pixels, int width, int height)
    {
        using var tiff = Tiff.Open(TestPath(name), "w")!;
        tiff.SetField(TiffTag.IMAGEWIDTH, width);
        tiff.SetField(TiffTag.IMAGELENGTH, height);
        tiff.SetField(TiffTag.SAMPLESPERPIXEL, 3);
        tiff.SetField(TiffTag.BITSPERSAMPLE, 8);
        tiff.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);
        tiff.SetField(TiffTag.PHOTOMETRIC, Photometric.RGB);
        tiff.SetField(TiffTag.ROWSPERSTRIP, height);

        var row = new byte[width * 3];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var (r, g, b) = pixels[(y * width) + x];
                row[(x * 3)] = r;
                row[(x * 3) + 1] = g;
                row[(x * 3) + 2] = b;
            }

            tiff.WriteScanline(row, y);
        }

        tiff.WriteDirectory();
    }

    private void WriteBilevel(string name, bool[] bits, int width)
    {
        using var tiff = Tiff.Open(TestPath(name), "w")!;
        tiff.SetField(TiffTag.IMAGEWIDTH, width);
        tiff.SetField(TiffTag.IMAGELENGTH, 1);
        tiff.SetField(TiffTag.SAMPLESPERPIXEL, 1);
        tiff.SetField(TiffTag.BITSPERSAMPLE, 1);
        tiff.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);
        tiff.SetField(TiffTag.PHOTOMETRIC, Photometric.MINISBLACK);
        tiff.SetField(TiffTag.ROWSPERSTRIP, 1);

        var row = new byte[(width + 7) / 8];
        for (var x = 0; x < width; x++)
        {
            if (bits[x])
            {
                row[x / 8] |= (byte)(0x80 >> (x % 8));
            }
        }

        tiff.WriteScanline(row, 0);
        tiff.WriteDirectory();
    }

    // ---- Tests -----------------------------------------------------------

    [Fact]
    public void Decodes16BitGreyscale()
    {
        // The IR channel files reported in the field are 16-bit greyscale.
        WriteGray16("gray16.tif", [0, 256, 32768, 65535], 4, 1);

        using var bitmap = ImageLoader.Decode(TestPath("gray16.tif"), 4, 1, ZoomMode.Fit);

        Assert.Equal(4, bitmap.Width);
        Assert.Equal(1, bitmap.Height);
        Assert.Equal(new SKColor(0, 0, 0, 255), bitmap.GetPixel(0, 0));
        Assert.Equal(new SKColor(1, 1, 1, 255), bitmap.GetPixel(1, 0));
        Assert.Equal(new SKColor(128, 128, 128, 255), bitmap.GetPixel(2, 0));
        Assert.Equal(new SKColor(255, 255, 255, 255), bitmap.GetPixel(3, 0));
    }

    [Fact]
    public void Decodes48BitRgb()
    {
        // The colour files reported in the field are 48-bit RGB.
        WriteRgb16("rgb48.tif",
            [(65535, 0, 0), (0, 65535, 0), (0, 0, 65535)],
            3, 1);

        using var bitmap = ImageLoader.Decode(TestPath("rgb48.tif"), 3, 1, ZoomMode.Fit);

        Assert.Equal(3, bitmap.Width);
        Assert.Equal(new SKColor(255, 0, 0, 255), bitmap.GetPixel(0, 0));
        Assert.Equal(new SKColor(0, 255, 0, 255), bitmap.GetPixel(1, 0));
        Assert.Equal(new SKColor(0, 0, 255, 255), bitmap.GetPixel(2, 0));
    }

    [Fact]
    public void Decodes8BitRgb()
    {
        WriteRgb8("rgb8.tif", [(10, 20, 30), (200, 150, 100)], 2, 1);

        using var bitmap = ImageLoader.Decode(TestPath("rgb8.tif"), 2, 1, ZoomMode.Fit);

        Assert.Equal(new SKColor(10, 20, 30, 255), bitmap.GetPixel(0, 0));
        Assert.Equal(new SKColor(200, 150, 100, 255), bitmap.GetPixel(1, 0));
    }

    [Fact]
    public void DecodesBilevel()
    {
        WriteBilevel("bilevel.tif", [true, false, true, false], 4);

        using var bitmap = ImageLoader.Decode(TestPath("bilevel.tif"), 4, 1, ZoomMode.Fit);

        Assert.Equal(new SKColor(255, 255, 255, 255), bitmap.GetPixel(0, 0));
        Assert.Equal(new SKColor(0, 0, 0, 255), bitmap.GetPixel(1, 0));
        Assert.Equal(new SKColor(255, 255, 255, 255), bitmap.GetPixel(2, 0));
        Assert.Equal(new SKColor(0, 0, 0, 255), bitmap.GetPixel(3, 0));
    }

    [Fact]
    public void AppliesOrientation6Rotation()
    {
        // Stored 3x2:
        //   A B C
        //   D E F
        // Orientation 6 (rotate 90 CW to display) => displayed 2x3:
        //   D A
        //   E B
        //   F C
        var a = ((byte)255, (byte)0, (byte)0);
        var b = ((byte)0, (byte)255, (byte)0);
        var c = ((byte)0, (byte)0, (byte)255);
        var d = ((byte)255, (byte)255, (byte)255);
        var e = ((byte)0, (byte)0, (byte)0);
        var f = ((byte)128, (byte)128, (byte)128);

        WriteRgb8("oriented.tif", [a, b, c, d, e, f], 3, 2);
        // Rewrite with orientation 6.
        using (var tiff = Tiff.Open(TestPath("oriented6.tif"), "w")!)
        {
            tiff.SetField(TiffTag.IMAGEWIDTH, 3);
            tiff.SetField(TiffTag.IMAGELENGTH, 2);
            tiff.SetField(TiffTag.SAMPLESPERPIXEL, 3);
            tiff.SetField(TiffTag.BITSPERSAMPLE, 8);
            tiff.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);
            tiff.SetField(TiffTag.PHOTOMETRIC, Photometric.RGB);
            tiff.SetField(TiffTag.ORIENTATION, TiffOrientation.RIGHTTOP);
            tiff.SetField(TiffTag.ROWSPERSTRIP, 2);
            var row = new byte[9];
            for (var x = 0; x < 3; x++)
            {
                var (r, g, bl) = new[] { a, b, c }[x];
                row[x * 3] = r; row[(x * 3) + 1] = g; row[(x * 3) + 2] = bl;
            }

            tiff.WriteScanline(row, 0);
            for (var x = 0; x < 3; x++)
            {
                var (r, g, bl) = new[] { d, e, f }[x];
                row[x * 3] = r; row[(x * 3) + 1] = g; row[(x * 3) + 2] = bl;
            }

            tiff.WriteScanline(row, 1);
            tiff.WriteDirectory();
        }

        using var bitmap = ImageLoader.Decode(TestPath("oriented6.tif"), 2, 3, ZoomMode.Fit);

        Assert.Equal(2, bitmap.Width);
        Assert.Equal(3, bitmap.Height);
        Assert.Equal(new SKColor(255, 255, 255, 255), bitmap.GetPixel(0, 0)); // D
        Assert.Equal(new SKColor(255, 0, 0, 255), bitmap.GetPixel(1, 0));     // A
        Assert.Equal(new SKColor(0, 0, 0, 255), bitmap.GetPixel(0, 1));       // E
        Assert.Equal(new SKColor(0, 255, 0, 255), bitmap.GetPixel(1, 1));     // B
        Assert.Equal(new SKColor(128, 128, 128, 255), bitmap.GetPixel(0, 2)); // F
        Assert.Equal(new SKColor(0, 0, 255, 255), bitmap.GetPixel(1, 2));     // C
    }

    [Fact]
    public void DownSamplesLargeImageToRequestedSize()
    {
        var width = 200;
        var height = 100;
        var pixels = new (byte R, byte G, byte B)[width * height];
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = (100, 150, 200);
        }

        WriteRgb8("large.tif", pixels, width, height);

        using var bitmap = ImageLoader.Decode(TestPath("large.tif"), 50, 25, ZoomMode.Fit);

        // 200/50 = 4 -> step 4 -> exactly 50x25.
        Assert.Equal(50, bitmap.Width);
        Assert.Equal(25, bitmap.Height);
        Assert.Equal(new SKColor(100, 150, 200, 255), bitmap.GetPixel(25, 12));
    }

    [Fact]
    public void ReadOrientedSizeSwapsDimensionsForQuarterTurns()
    {
        WriteGray16("rot.tif", new ushort[4 * 2], 4, 2, TiffOrientation.RIGHTTOP);
        var size = ImageLoader.ReadOrientedSize(TestPath("rot.tif"));
        Assert.Equal(2, size.Width);
        Assert.Equal(4, size.Height);
    }

    [Fact]
    public void SupportedExtensions_IncludeTiff()
    {
        Assert.True(FolderScanner.IsSupportedImage("photo.tif"));
        Assert.True(FolderScanner.IsSupportedImage("photo.TIFF"));
        Assert.False(FolderScanner.IsSupportedImage("photo.jpg.txt"));
    }
}
