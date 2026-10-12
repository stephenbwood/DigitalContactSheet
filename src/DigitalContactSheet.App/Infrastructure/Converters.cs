using System.Globalization;
using System.Text;
using Avalonia.Data.Converters;
using Avalonia.Media;
using DigitalContactSheet.Core.Model;

namespace DigitalContactSheet.App.Infrastructure;

/// <summary>Renders enum values as friendly labels in combo boxes.</summary>
public sealed class EnumDisplayConverter : IValueConverter
{
    private static readonly Dictionary<string, string> Overrides = new()
    {
        ["MirrorUnderOutputDirectory"] = "Mirror under output folder",
        ["IntoSourceFolder"] = "Into each source folder",
        ["NameNatural"] = "File name (natural)",
        ["DateModified"] = "Date modified",
        ["Size"] = "File size",
        ["Jpg"] = "JPEG",
        ["Tiff"] = "TIFF",
        ["Pdf"] = "PDF",
        ["A5"] = "A5",
        ["A4"] = "A4",
        ["A3"] = "A3",
        ["Mm"] = "mm",
        ["In"] = "in",
        ["Pt"] = "pt",
    };

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return string.Empty;
        }

        var name = value.ToString() ?? string.Empty;
        if (Overrides.TryGetValue(name, out var friendly))
        {
            return friendly;
        }

        var builder = new StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1]))
            {
                builder.Append(' ');
            }

            builder.Append(c);
        }

        return builder.ToString();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Avalonia.Data.BindingOperations.DoNothing;
}

/// <summary>Converts a hex colour string into a brush for a preview swatch.</summary>
public sealed class HexToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string text && RgbaColor.TryParse(text, out var color))
        {
            return new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));
        }

        return Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Avalonia.Data.BindingOperations.DoNothing;
}
