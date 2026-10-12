using System.Collections.Concurrent;
using DigitalContactSheet.Core.Model;
using SkiaSharp;

namespace DigitalContactSheet.Core.Rendering;

/// <summary>
/// Resolves <see cref="SKTypeface"/> instances for a family name and weight, preferring the
/// fonts bundled with the application so output is identical on every platform, and falling
/// back to system-installed fonts.
/// </summary>
public static class FontProvider
{
    /// <summary>The family name exposed by the bundled fonts.</summary>
    public const string BundledFamily = "Inter";

    private const string ResourcePrefix = "DigitalContactSheet.Core.Fonts.";
    private const int NormalWidth = 400;

    private static readonly Dictionary<FontWeightOption, string> BundledFiles = new()
    {
        [FontWeightOption.Light] = "Inter-Light.ttf",
        [FontWeightOption.Regular] = "Inter-Regular.ttf",
        [FontWeightOption.Medium] = "Inter-Medium.ttf",
        [FontWeightOption.Bold] = "Inter-Bold.ttf",
        [FontWeightOption.Black] = "Inter-Black.ttf",
    };

    private static readonly ConcurrentDictionary<string, SKTypeface> Cache = new();

    /// <summary>Family names that are always available because they ship with the app.</summary>
    public static IReadOnlyList<string> BundledFamilies { get; } = [BundledFamily];

    /// <summary>All family names available from the bundled set plus the host system.</summary>
    public static IReadOnlyList<string> GetAvailableFamilies()
    {
        var families = new SortedSet<string>(StringComparer.OrdinalIgnoreCase) { BundledFamily };
        try
        {
            foreach (var family in SKFontManager.Default.GetFontFamilies())
            {
                if (!string.IsNullOrWhiteSpace(family))
                {
                    families.Add(family);
                }
            }
        }
        catch
        {
            // Font enumeration is best-effort; the bundled family is always present.
        }

        return families.ToList();
    }

    /// <summary>Resolves the best typeface for the requested family and weight.</summary>
    public static SKTypeface Resolve(string family, FontWeightOption weight)
    {
        family = string.IsNullOrWhiteSpace(family) ? BundledFamily : family.Trim();
        return Cache.GetOrAdd($"{family}|{weight}", _ => ResolveCore(family, weight));
    }

    private static SKTypeface ResolveCore(string family, FontWeightOption weight)
    {
        if (string.Equals(family, BundledFamily, StringComparison.OrdinalIgnoreCase))
        {
            var bundled = LoadBundled(weight);
            if (bundled is not null)
            {
                return bundled;
            }
        }

        var numericWeight = ContactSheetOptions.WeightToNumeric(weight);
        var style = new SKFontStyle(numericWeight, NormalWidth, SKFontStyleSlant.Upright);

        var system = SKFontManager.Default.MatchFamily(family, style);
        if (system is not null)
        {
            return system;
        }

        // Fall back to the bundled regular face, then to the platform default.
        return LoadBundled(FontWeightOption.Regular) ?? SKTypeface.Default;
    }

    private static SKTypeface? LoadBundled(FontWeightOption weight)
    {
        if (!BundledFiles.TryGetValue(weight, out var file))
        {
            file = BundledFiles[FontWeightOption.Regular];
        }

        var data = ReadResource(file);
        return SKTypeface.FromData(SKData.CreateCopy(data), 0);
    }

    private static byte[] ReadResource(string fileName)
    {
        var assembly = typeof(FontProvider).Assembly;
        var name = ResourcePrefix + fileName;
        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Bundled font resource '{name}' was not found.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
