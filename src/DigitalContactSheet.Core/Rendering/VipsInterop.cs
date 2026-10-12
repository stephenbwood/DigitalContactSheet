using NetVips;
using SkiaSharp;

namespace DigitalContactSheet.Core.Rendering;

/// <summary>
/// Converts between libvips images (used for decode/encode) and SkiaSharp bitmaps (used for
/// composition and PDF output). Pixels are exchanged as 8-bit sRGB.
/// </summary>
internal static class VipsInterop
{
    /// <summary>
    /// Converts a decoded libvips image to an <see cref="SKBitmap"/>. The image is expected to be
    /// 8-bit already; greyscale and CMYK are converted to sRGB, and alpha is preserved.
    /// </summary>
    public static SKBitmap ToSkia(Image image)
    {
        var temporaries = new List<Image>();
        try
        {
            var source = Normalise(image, temporaries);

            var width = source.Width;
            var height = source.Height;
            var bands = source.Bands;
            var raw = source.WriteToBuffer(".raw");

            // RGBA in memory maps onto Skia's Rgba8888 colour type.
            var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            var bitmap = new SKBitmap(info);
            var destination = bitmap.GetPixelSpan();
            var destinationRowBytes = bitmap.RowBytes;

            if (bands == 4)
            {
                var sourceRowBytes = width * 4;
                for (var y = 0; y < height; y++)
                {
                    raw.AsSpan(y * sourceRowBytes, sourceRowBytes)
                        .CopyTo(destination.Slice(y * destinationRowBytes, sourceRowBytes));
                }
            }
            else
            {
                // 3-band RGB -> expand to RGBA with an opaque alpha channel.
                var sourceRowBytes = width * 3;
                for (var y = 0; y < height; y++)
                {
                    var src = y * sourceRowBytes;
                    var dst = y * destinationRowBytes;
                    for (var x = 0; x < width; x++)
                    {
                        destination[dst] = raw[src];
                        destination[dst + 1] = raw[src + 1];
                        destination[dst + 2] = raw[src + 2];
                        destination[dst + 3] = 255;
                        src += 3;
                        dst += 4;
                    }
                }
            }

            return bitmap;
        }
        finally
        {
            foreach (var temporary in temporaries)
            {
                temporary.Dispose();
            }
        }
    }

    /// <summary>Builds a 3-band sRGB libvips image from a rendered <see cref="SKBitmap"/>.</summary>
    public static Image FromSkia(SKBitmap bitmap)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;
        var pixels = new byte[width * height * 3];
        var source = bitmap.GetPixelSpan();
        var rowBytes = bitmap.RowBytes;

        for (var y = 0; y < height; y++)
        {
            var srcRow = y * rowBytes;
            var dstRow = y * width * 3;
            for (var x = 0; x < width; x++)
            {
                var i = srcRow + (x * 4); // BGRA, premultiplied
                var b = source[i];
                var g = source[i + 1];
                var r = source[i + 2];
                var a = source[i + 3];

                if (a != 255)
                {
                    if (a == 0)
                    {
                        r = g = b = 0;
                    }
                    else
                    {
                        r = (byte)Math.Min(255, r * 255 / a);
                        g = (byte)Math.Min(255, g * 255 / a);
                        b = (byte)Math.Min(255, b * 255 / a);
                    }
                }

                var o = dstRow + (x * 3);
                pixels[o] = r;
                pixels[o + 1] = g;
                pixels[o + 2] = b;
            }
        }

        return Image.NewFromMemory(pixels, width, height, 3, Enums.BandFormat.Uchar);
    }

    /// <summary>Normalises an image to 8-bit, 3 or 4 bands, sRGB colour.</summary>
    private static Image Normalise(Image image, List<Image> temporaries)
    {
        var source = image;

        if (source.Format != Enums.BandFormat.Uchar)
        {
            // shift: true scales 16-bit values down (>> 8) instead of clamping them to white.
            source = Keep(source.Cast(Enums.BandFormat.Uchar, true), source, temporaries);
        }

        switch (source.Bands)
        {
            case 1:
                // Greyscale / bilevel -> sRGB (3 bands).
                source = Keep(source.Colourspace(Enums.Interpretation.Srgb), source, temporaries);
                break;

            case 2:
            {
                // Greyscale + alpha.
                var grey = Keep(source.ExtractBand(0, 1).Colourspace(Enums.Interpretation.Srgb), source, temporaries);
                var alpha = Keep(source.ExtractBand(1, 1), source, temporaries);
                source = Keep(grey.Bandjoin(alpha), source, temporaries);
                break;
            }

            case 4:
                // CMYK must be converted to RGB; 4-band RGB(A) is left alone.
                if (source.Interpretation == Enums.Interpretation.Cmyk)
                {
                    source = Keep(source.Colourspace(Enums.Interpretation.Srgb), source, temporaries);
                }

                break;

            case 3:
                break;

            default:
                throw new ImageLoadException($"Unsupported number of colour bands ({source.Bands}).");
        }

        if (source.Bands is not (3 or 4))
        {
            throw new ImageLoadException($"Unexpected colour band count after conversion ({source.Bands}).");
        }

        return source;
    }

    /// <summary>Registers a derived image for disposal, unless the operation returned its input.</summary>
    private static Image Keep(Image result, Image input, List<Image> temporaries)
    {
        if (!ReferenceEquals(result, input))
        {
            temporaries.Add(result);
        }

        return result;
    }
}
