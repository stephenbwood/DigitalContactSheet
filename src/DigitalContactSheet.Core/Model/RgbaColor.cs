using System.Globalization;

namespace DigitalContactSheet.Core.Model;

/// <summary>A simple 8-bit-per-channel RGBA colour, independent of any rendering library.</summary>
public readonly record struct RgbaColor(byte R, byte G, byte B, byte A = 255)
{
    public static readonly RgbaColor White = new(255, 255, 255);
    public static readonly RgbaColor Black = new(0, 0, 0);

    /// <summary>
    /// Parses <c>#RGB</c>, <c>#RGBA</c>, <c>#RRGGBB</c> or <c>#RRGGBBAA</c> (the leading
    /// <c>#</c> is optional). Throws <see cref="FormatException"/> on malformed input.
    /// </summary>
    public static RgbaColor Parse(string text)
    {
        if (!TryParse(text, out var color))
        {
            throw new FormatException($"'{text}' is not a valid hex colour.");
        }

        return color;
    }

    public static bool TryParse(string? text, out RgbaColor color)
    {
        color = Black;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var hex = text.Trim();
        if (hex.StartsWith('#'))
        {
            hex = hex[1..];
        }

        if (hex.Length is not (3 or 4 or 6 or 8))
        {
            return false;
        }

        if (!uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        switch (hex.Length)
        {
            case 3:
            {
                var r = (byte)(((value >> 8) & 0xF) * 17);
                var g = (byte)(((value >> 4) & 0xF) * 17);
                var b = (byte)((value & 0xF) * 17);
                color = new RgbaColor(r, g, b);
                return true;
            }

            case 4:
            {
                var r = (byte)(((value >> 12) & 0xF) * 17);
                var g = (byte)(((value >> 8) & 0xF) * 17);
                var b = (byte)(((value >> 4) & 0xF) * 17);
                var a = (byte)((value & 0xF) * 17);
                color = new RgbaColor(r, g, b, a);
                return true;
            }

            case 6:
            {
                var r = (byte)((value >> 16) & 0xFF);
                var g = (byte)((value >> 8) & 0xFF);
                var b = (byte)(value & 0xFF);
                color = new RgbaColor(r, g, b);
                return true;
            }

            default:
            {
                var r = (byte)((value >> 24) & 0xFF);
                var g = (byte)((value >> 16) & 0xFF);
                var b = (byte)((value >> 8) & 0xFF);
                var a = (byte)(value & 0xFF);
                color = new RgbaColor(r, g, b, a);
                return true;
            }
        }
    }

    /// <summary>Formats the colour as <c>#RRGGBB</c> (dropping alpha when fully opaque).</summary>
    public string ToHex() => A == 255
        ? $"#{R:X2}{G:X2}{B:X2}"
        : $"#{R:X2}{G:X2}{B:X2}{A:X2}";
}
