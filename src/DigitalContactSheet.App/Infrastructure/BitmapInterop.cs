using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;

namespace DigitalContactSheet.App.Infrastructure;

/// <summary>Converts a SkiaSharp bitmap into an Avalonia bitmap for display.</summary>
public static class BitmapInterop
{
    /// <summary>
    /// Copies the pixels straight into a <see cref="WriteableBitmap"/> (a memcpy per row) rather
    /// than round-tripping through PNG, which is several times slower and blocks the UI thread.
    /// Falls back to PNG encoding for pixel formats that cannot be copied directly.
    /// </summary>
    public static Bitmap ToAvaloniaBitmap(SKBitmap bitmap)
    {
        if (TryGetPixelFormat(bitmap.ColorType, bitmap.AlphaType, out var pixelFormat, out var alphaFormat))
        {
            var writeable = new WriteableBitmap(
                new PixelSize(bitmap.Width, bitmap.Height),
                new Vector(96, 96),
                pixelFormat,
                alphaFormat);

            var source = bitmap.Bytes;
            var sourceRowBytes = bitmap.RowBytes;
            var copyBytes = bitmap.Width * 4;

            using (var framebuffer = writeable.Lock())
            {
                for (var y = 0; y < bitmap.Height; y++)
                {
                    Marshal.Copy(source, y * sourceRowBytes, framebuffer.Address + (y * framebuffer.RowBytes), copyBytes);
                }
            }

            return writeable;
        }

        // Fallback: encode to PNG and let Avalonia decode it.
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = new MemoryStream(data.ToArray());
        return new Bitmap(stream);
    }

    private static bool TryGetPixelFormat(SKColorType colorType, SKAlphaType alphaType, out PixelFormat format, out AlphaFormat alphaFormat)
    {
        alphaFormat = alphaType switch
        {
            SKAlphaType.Opaque => AlphaFormat.Opaque,
            SKAlphaType.Premul => AlphaFormat.Premul,
            _ => AlphaFormat.Unpremul,
        };

        switch (colorType)
        {
            case SKColorType.Rgba8888:
                format = PixelFormat.Rgba8888;
                return true;

            case SKColorType.Bgra8888:
                format = PixelFormat.Bgra8888;
                return true;

            default:
                format = default;
                return false;
        }
    }
}
