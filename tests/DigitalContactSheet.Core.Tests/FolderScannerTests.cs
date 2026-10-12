using DigitalContactSheet.Core.Model;
using DigitalContactSheet.Core.Scanning;

namespace DigitalContactSheet.Core.Tests;

public class FolderScannerTests : IDisposable
{
    private readonly string _root;

    public FolderScannerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "dcs-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }

    private string Touch(string relativePath, string content = "x")
    {
        var full = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        return full;
    }

    [Fact]
    public void GetImages_OrdersNaturallyAndFiltersExtensions()
    {
        Touch("img2.jpg");
        Touch("img10.jpg");
        Touch("img1.jpg");
        Touch("notes.txt");

        var images = FolderScanner.GetImages(_root, SortOrder.NameNatural);

        Assert.Equal(["img1.jpg", "img2.jpg", "img10.jpg"], images.Select(i => i.FileName));
    }

    [Fact]
    public void GetImages_SortsBySizeDescending()
    {
        Touch("small.jpg", "1");
        Touch("large.jpg", new string('x', 100));
        Touch("medium.jpg", new string('x', 10));

        var images = FolderScanner.GetImages(_root, SortOrder.Size);

        Assert.Equal(["large.jpg", "medium.jpg", "small.jpg"], images.Select(i => i.FileName));
    }

    [Fact]
    public void FindFoldersWithImages_NonRecursive_ReturnsOnlyRoot()
    {
        Touch("a.png");
        Touch("sub/b.png");

        var folders = FolderScanner.FindFoldersWithImages(_root, recurse: false, SortOrder.NameNatural);

        Assert.Single(folders);
        Assert.Equal(_root, folders[0].Path);
        Assert.Equal(1, folders[0].ImageCount);
    }

    [Fact]
    public void FindFoldersWithImages_Recursive_ReturnsEachFolderWithImages()
    {
        Touch("a.png");
        Touch("sub1/b.png");
        Touch("sub1/c.png");
        Touch("sub2/d.png");
        Directory.CreateDirectory(Path.Combine(_root, "empty"));

        var folders = FolderScanner.FindFoldersWithImages(_root, recurse: true, SortOrder.NameNatural);

        Assert.Equal(3, folders.Count);
        Assert.Contains(folders, f => f.ImageCount == 2);
        Assert.DoesNotContain(folders, f => f.Path.EndsWith("empty"));
    }

    [Fact]
    public void FindFoldersWithImages_MissingFolder_ReturnsEmpty()
    {
        var folders = FolderScanner.FindFoldersWithImages(Path.Combine(_root, "does-not-exist"), true, SortOrder.NameNatural);
        Assert.Empty(folders);
    }
}

public class NaturalComparerTests
{
    [Theory]
    [InlineData("img2", "img10", -1)]
    [InlineData("img10", "img2", 1)]
    [InlineData("IMG2", "img10", -1)]
    [InlineData("a", "a", 0)]
    [InlineData("a2b", "a10b", -1)]
    [InlineData("007", "8", -1)]
    public void Compare_OrdersNaturally(string x, string y, int expectedSign)
    {
        var result = Math.Sign(NaturalComparer.Instance.Compare(x, y));
        Assert.Equal(expectedSign, result);
    }
}
