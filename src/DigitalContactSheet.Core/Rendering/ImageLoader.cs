using DigitalContactSheet.Core.Model;
using NetVips;
using SkiaSharp;

namespace DigitalContactSheet.Core.Rendering;

/// <summary>Raised when an image file cannot be decoded.</summary>
public sealed class ImageLoadException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Decodes image files with libvips, down-sampling them to the size actually required by the
/// layout (so a huge source image is never held at full resolution) and honouring EXIF/TIFF
/// orientation. libvips covers the full format matrix — 8/16-bit, 48-bit RGB, CMYK, palette,
/// bilevel, tiled, planar and compressed TIFF — that SkiaSharp cannot.
/// </summary>
public static class ImageLoader
{
    /// <summary>
    /// Decodes the image at <paramref name="path"/> so that it is no larger than needed to fill
    /// or fit <paramref name="areaWidth"/> x <paramref name="areaHeight"/> under the given zoom
    /// mode. The returned bitmap is already oriented correctly.
    /// </summary>
    public static SKBitmap Decode(string path, double areaWidth, double areaHeight, ZoomMode mode)
    {
        areaWidth = Math.Max(1, areaWidth);
        areaHeight = Math.Max(1, areaHeight);

        try
        {
            using var file = Image.NewFromFile(path, access: Enums.Access.Sequential);
            using var oriented = file.Autorot();

            var (width, height) = ComputeTargetSize(oriented.Width, oriented.Height, areaWidth, areaHeight, mode);
            return Render(oriented, width, height);
        }
        catch (ImageLoadException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ImageLoadException($"Failed to load '{Path.GetFileName(path)}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Decodes the image at exactly <paramref name="width"/> x <paramref name="height"/> (which must
    /// preserve the source aspect ratio). Used by the cache to decode once at a canonical size.
    /// </summary>
    public static SKBitmap DecodeToSize(string path, int width, int height)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);

        try
        {
            using var file = Image.NewFromFile(path, access: Enums.Access.Sequential);
            using var oriented = file.Autorot();
            return Render(oriented, width, height);
        }
        catch (ImageLoadException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ImageLoadException($"Failed to load '{Path.GetFileName(path)}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// The pixel size an image of <paramref name="orientedWidth"/> x <paramref name="orientedHeight"/>
    /// should be decoded at to fit (<see cref="ZoomMode.Fit"/>) or cover
    /// (<see cref="ZoomMode.Fill"/>) the supplied area. Never up-scales.
    /// </summary>
    public static (int Width, int Height) ComputeTargetSize(
        int orientedWidth,
        int orientedHeight,
        double areaWidth,
        double areaHeight,
        ZoomMode mode)
    {
        var scaleX = areaWidth / orientedWidth;
        var scaleY = areaHeight / orientedHeight;
        var scale = mode == ZoomMode.Fill ? Math.Max(scaleX, scaleY) : Math.Min(scaleX, scaleY);
        scale = Math.Min(1.0, scale);
        if (scale <= 0 || double.IsNaN(scale))
        {
            scale = 1.0;
        }

        return (Math.Max(1, (int)Math.Round(orientedWidth * scale)), Math.Max(1, (int)Math.Round(orientedHeight * scale)));
    }

    /// <summary>Reads the image dimensions after orientation is applied, without decoding pixels.</summary>
    public static SKSizeI ReadOrientedSize(string path)
    {
        try
        {
            using var file = Image.NewFromFile(path, access: Enums.Access.Sequential);
            using var oriented = file.Autorot();
            return new SKSizeI(oriented.Width, oriented.Height);
        }
        catch (Exception ex)
        {
            throw new ImageLoadException($"Failed to read '{Path.GetFileName(path)}': {ex.Message}", ex);
        }
    }

    private static SKBitmap Render(Image oriented, int width, int height)
    {
        // ThumbnailImage uses libvips' shrink-on-load paths (fast for JPEG) and, because the
        // target preserves the source aspect ratio, "both" yields exactly the requested size.
        using var sized = oriented.ThumbnailImage(width, height, size: Enums.Size.Both, crop: Enums.Interesting.None);
        using var eightBit = sized.Cast(Enums.BandFormat.Uchar, true);
        return VipsInterop.ToSkia(eightBit);
    }
}
