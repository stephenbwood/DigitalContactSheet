using DigitalContactSheet.Core.Layout;
using DigitalContactSheet.Core.Model;
using DigitalContactSheet.Core.Scanning;

namespace DigitalContactSheet.Core.Export;

/// <summary>A single folder's planned output.</summary>
public sealed record ExportJob(
    SourceFolder Folder,
    string DestinationDirectory,
    PageMetrics Metrics);

/// <summary>Describes what an export run will produce, without touching the disk.</summary>
public sealed record ExportPlan(
    string RootFolder,
    ContactSheetOptions Options,
    IReadOnlyList<ExportJob> Jobs)
{
    public int TotalImages => Jobs.Sum(j => j.Folder.ImageCount);

    public int TotalPages => Jobs.Sum(j => j.Metrics.PageCount);

    public int TotalFiles => Options.OutputFormat == OutputFormat.Pdf
        ? Jobs.Count(j => j.Metrics.PageCount > 0)
        : TotalPages;
}

/// <summary>Builds an <see cref="ExportPlan"/> from options and a root folder.</summary>
public static class ExportPlanner
{
    public static ExportPlan Build(string rootFolder, ContactSheetOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var folders = FolderScanner.FindFoldersWithImages(rootFolder, options.RecurseSubfolders, options.SortOrder);

        var jobs = new List<ExportJob>(folders.Count);
        foreach (var folder in folders)
        {
            var metrics = LayoutEngine.Compute(options, folder.ImageCount);
            var destination = ExportNaming.ResolveDestination(options, rootFolder, folder.Path);
            jobs.Add(new ExportJob(folder, destination, metrics));
        }

        return new ExportPlan(rootFolder, options.Clone(), jobs);
    }
}
