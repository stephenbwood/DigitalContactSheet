namespace DigitalContactSheet.Core.Scanning;

/// <summary>A single image discovered on disk.</summary>
public sealed record ImageFile(string Path, string FileName, long SizeBytes, DateTime LastModifiedUtc)
{
    public string Extension => System.IO.Path.GetExtension(FileName);
}

/// <summary>A folder that contains at least one supported image.</summary>
public sealed record SourceFolder(string Path, IReadOnlyList<ImageFile> Images)
{
    public string Name => System.IO.Path.GetFileName(Path.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar));

    public int ImageCount => Images.Count;
}
