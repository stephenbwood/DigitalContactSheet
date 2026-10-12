using DigitalContactSheet.Core.Model;

namespace DigitalContactSheet.Core.Export;

/// <summary>Builds output file names and destination folders.</summary>
public static class ExportNaming
{
    public static string ExtensionFor(OutputFormat format) => format switch
    {
        OutputFormat.Jpg => ".jpg",
        OutputFormat.Tiff => ".tif",
        _ => ".pdf",
    };

    /// <summary>File name for the multi-page PDF output.</summary>
    public static string PdfFileName(string prefix)
    {
        var safe = SanitizePrefix(prefix);
        return $"{safe}.pdf";
    }

    /// <summary>
    /// File name for a single raster page, e.g. <c>contact-sheet-001.jpg</c>. Page numbers are
    /// 1-based and zero-padded.
    /// </summary>
    public static string RasterFileName(string prefix, int pageIndex, int digits, OutputFormat format)
    {
        var safe = SanitizePrefix(prefix);
        var pageNumber = pageIndex + 1;
        var width = Math.Clamp(digits, 1, 10);
        var number = pageNumber.ToString(new string('0', width));
        return $"{safe}-{number}{ExtensionFor(format)}";
    }

    /// <summary>
    /// Resolves the directory a folder's output should be written to. When mirroring, the source
    /// folder's path relative to <paramref name="rootFolder"/> is recreated beneath
    /// <paramref name="outputRoot"/>.
    /// </summary>
    public static string ResolveDestination(ContactSheetOptions options, string rootFolder, string sourceFolder)
    {
        if (options.OutputMode == OutputMode.IntoSourceFolder)
        {
            return sourceFolder;
        }

        if (string.IsNullOrWhiteSpace(options.OutputDirectory))
        {
            return sourceFolder;
        }

        var relative = Path.GetRelativePath(rootFolder, sourceFolder);
        if (relative is "." or "")
        {
            return options.OutputDirectory;
        }

        return Path.Combine(options.OutputDirectory, relative);
    }

    private static string SanitizePrefix(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return "contact-sheet";
        }

        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(prefix.Select(c => invalid.Contains(c) ? '-' : c).ToArray()).Trim();
        return string.IsNullOrEmpty(cleaned) ? "contact-sheet" : cleaned;
    }
}
