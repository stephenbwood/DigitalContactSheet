using DigitalContactSheet.Core.Model;
using DigitalContactSheet.Core.Rendering;
using DigitalContactSheet.Core.Scanning;
using NetVips;
using SkiaSharp;

namespace DigitalContactSheet.Core.Export;

/// <summary>Progress notification emitted while an export runs.</summary>
public sealed record ExportProgress(
    string FolderPath,
    int FolderIndex,
    int FolderCount,
    int PageIndex,
    int PageCount,
    string Message);

/// <summary>The result of an export run.</summary>
public sealed record ExportSummary(
    int FolderCount,
    int PageCount,
    int FileCount,
    IReadOnlyList<string> Files);

/// <summary>Renders an <see cref="ExportPlan"/> to disk, one folder at a time.</summary>
public sealed class ExportService
{
    private readonly PageRenderer _renderer = new();

    public ExportSummary Export(
        ExportPlan plan,
        IProgress<ExportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var options = plan.Options;
        var files = new List<string>();
        var totalPages = 0;
        var foldersProcessed = 0;
        var folderCount = plan.Jobs.Count;

        for (var folderIndex = 0; folderIndex < plan.Jobs.Count; folderIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var job = plan.Jobs[folderIndex];
            if (job.Metrics.PageCount == 0)
            {
                continue;
            }

            Directory.CreateDirectory(job.DestinationDirectory);

            var produced = options.OutputFormat == OutputFormat.Pdf
                ? ExportPdf(job, options, progress, folderIndex, folderCount, cancellationToken)
                : ExportRasters(job, options, progress, folderIndex, folderCount, cancellationToken);

            files.AddRange(produced);
            totalPages += job.Metrics.PageCount;
            foldersProcessed++;
        }

        return new ExportSummary(foldersProcessed, totalPages, files.Count, files);
    }

    private List<string> ExportPdf(
        ExportJob job,
        ContactSheetOptions options,
        IProgress<ExportProgress>? progress,
        int folderIndex,
        int folderCount,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(job.DestinationDirectory, ExportNaming.PdfFileName(options.FileNamePrefix));

        using (var stream = File.Create(path))
        using (var document = SKDocument.CreatePdf(stream, (float)options.Resolution))
        {
            for (var page = 0; page < job.Metrics.PageCount; page++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _renderer.RenderPageToDocument(document, job.Metrics, job.Folder.Images, page, options);
                progress?.Report(new ExportProgress(
                    job.Folder.Path, folderIndex, folderCount, page + 1, job.Metrics.PageCount, $"Rendering {Path.GetFileName(path)}"));
            }

            document.Close();
        }

        return [path];
    }

    private List<string> ExportRasters(
        ExportJob job,
        ContactSheetOptions options,
        IProgress<ExportProgress>? progress,
        int folderIndex,
        int folderCount,
        CancellationToken cancellationToken)
    {
        var produced = new List<string>(job.Metrics.PageCount);

        for (var page = 0; page < job.Metrics.PageCount; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var bitmap = _renderer.RenderToBitmap(job.Metrics, job.Folder.Images, page, options);
            var fileName = ExportNaming.RasterFileName(options.FileNamePrefix, page, options.PageNumberDigits, options.OutputFormat);
            var path = Path.Combine(job.DestinationDirectory, fileName);

            using (var encoded = VipsInterop.FromSkia(bitmap))
            {
                if (options.OutputFormat == OutputFormat.Tiff)
                {
                    encoded.WriteToFile(path, new VOption
                    {
                        { "compression", Enums.ForeignTiffCompression.Lzw },
                        { "predictor", Enums.ForeignTiffPredictor.Horizontal },
                    });
                }
                else
                {
                    encoded.WriteToFile(path, new VOption { { "Q", Math.Clamp(options.JpgQuality, 1, 100) } });
                }
            }

            produced.Add(path);
            progress?.Report(new ExportProgress(
                job.Folder.Path, folderIndex, folderCount, page + 1, job.Metrics.PageCount, $"Rendering {fileName}"));
        }

        return produced;
    }
}
