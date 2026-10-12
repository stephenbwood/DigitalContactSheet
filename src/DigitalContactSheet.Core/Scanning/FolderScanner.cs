using DigitalContactSheet.Core.Model;

namespace DigitalContactSheet.Core.Scanning;

/// <summary>Discovers image files in folders and groups them for batch processing.</summary>
public static class FolderScanner
{
    /// <summary>File extensions (lower-case, with dot) recognised as images.</summary>
    public static readonly IReadOnlySet<string> SupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".tif", ".tiff", ".webp", ".bmp", ".gif",
    };

    public static bool IsSupportedImage(string path)
    {
        var ext = Path.GetExtension(path);
        return !string.IsNullOrEmpty(ext) && SupportedExtensions.Contains(ext);
    }

    /// <summary>Enumerates the supported images directly inside <paramref name="folder"/>, sorted.</summary>
    public static IReadOnlyList<ImageFile> GetImages(string folder, SortOrder sortOrder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return [];
        }

        var files = Directory.EnumerateFiles(folder)
            .Where(IsSupportedImage)
            .Select(ToImageFile)
            .ToList();

        Sort(files, sortOrder);
        return files;
    }

    /// <summary>
    /// Returns the folders to process. When <paramref name="recurse"/> is false this is just the
    /// root (if it holds images); otherwise every folder at or beneath the root that holds images.
    /// Folders without images are omitted.
    /// </summary>
    public static IReadOnlyList<SourceFolder> FindFoldersWithImages(string root, bool recurse, SortOrder sortOrder)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            return [];
        }

        var folders = new List<SourceFolder>();

        void Consider(string path)
        {
            var images = GetImages(path, sortOrder);
            if (images.Count > 0)
            {
                folders.Add(new SourceFolder(path, images));
            }
        }

        Consider(root);

        if (recurse)
        {
            var option = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint,
            };

            var subdirectories = Directory.EnumerateDirectories(root, "*", option)
                .OrderBy(p => p, NaturalComparer.Instance);

            foreach (var dir in subdirectories)
            {
                Consider(dir);
            }
        }

        return folders;
    }

    private static ImageFile ToImageFile(string path)
    {
        var info = new FileInfo(path);
        return new ImageFile(info.FullName, info.Name, info.Length, info.LastWriteTimeUtc);
    }

    private static void Sort(List<ImageFile> files, SortOrder sortOrder)
    {
        switch (sortOrder)
        {
            case SortOrder.DateModified:
                files.Sort((a, b) => b.LastModifiedUtc.CompareTo(a.LastModifiedUtc));
                break;

            case SortOrder.Size:
                files.Sort((a, b) => b.SizeBytes.CompareTo(a.SizeBytes));
                break;

            default:
                files.Sort((a, b) => NaturalComparer.Instance.Compare(a.FileName, b.FileName));
                break;
        }
    }
}
