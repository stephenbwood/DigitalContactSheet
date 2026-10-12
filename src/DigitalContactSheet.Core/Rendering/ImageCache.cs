using DigitalContactSheet.Core.Model;
using SkiaSharp;

namespace DigitalContactSheet.Core.Rendering;

/// <summary>
/// An LRU cache of decoded bitmaps used by the live preview.
/// <para>
/// Entries are keyed by file identity only (not by target size). Each image is decoded once at a
/// <em>canonical</em> size — a generous bounding box — and any smaller request is served from that
/// bitmap, letting Skia scale it down when drawing. Decoding cost is dominated by reading and
/// converting the source file and is almost independent of the target size, so this trades a little
/// memory for near-instant re-rendering when the layout changes.
/// </para>
/// <para>
/// Exports do not use the cache: each image is needed once and holding them all would be wasteful.
/// </para>
/// </summary>
public sealed class ImageCache
{
    /// <summary>Default canonical bounding box, in pixels, that images are decoded to fit within.</summary>
    public const int DefaultCanonicalBox = 1024;

    private readonly int _maxEntries;
    private readonly long _maxBytes;
    private readonly int _canonicalBox;

    private readonly Dictionary<string, Entry> _index = new(StringComparer.Ordinal);
    private readonly LinkedList<Entry> _order = new();
    private readonly List<SKBitmap> _pendingDispose = [];
    private readonly Lock _gate = new();

    private long _bytes;
    private int _pinCount;

    public ImageCache(
        int maxEntries = 64,
        long maxBytes = 160L * 1024 * 1024,
        int canonicalBox = DefaultCanonicalBox)
    {
        _maxEntries = Math.Max(1, maxEntries);
        _maxBytes = Math.Max(1, maxBytes);
        _canonicalBox = Math.Max(1, canonicalBox);
    }

    /// <summary>Number of entries currently held.</summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _index.Count;
            }
        }
    }

    /// <summary>Approximate number of bytes of decoded pixel data currently held.</summary>
    public long ApproximateBytes
    {
        get
        {
            lock (_gate)
            {
                return _bytes;
            }
        }
    }

    /// <summary>Bitmaps evicted while pinned, awaiting disposal. Exposed for testing.</summary>
    internal int PendingDisposalCount
    {
        get
        {
            lock (_gate)
            {
                return _pendingDispose.Count;
            }
        }
    }

    /// <summary>
    /// Prevents evicted bitmaps from being disposed while a render is using them. Bitmaps evicted
    /// during a pin are released when the outermost pin is disposed.
    /// </summary>
    public IDisposable Pin()
    {
        lock (_gate)
        {
            _pinCount++;
        }

        return new PinScope(this);
    }

    /// <summary>
    /// Returns a decoded bitmap at least as large as the request needs.
    /// <paramref name="owned"/> is true only when the caller must dispose the bitmap (it was too
    /// large to cache); otherwise the cache owns it.
    /// </summary>
    public SKBitmap Get(string path, double areaWidth, double areaHeight, ZoomMode mode, out bool owned)
    {
        var key = BuildKey(path);

        // Fast path: an existing entry that already covers the request.
        lock (_gate)
        {
            if (_index.TryGetValue(key, out var entry))
            {
                var (requiredWidth, requiredHeight) = ImageLoader.ComputeTargetSize(
                    entry.SourceWidth, entry.SourceHeight, areaWidth, areaHeight, mode);

                if (entry.Width >= requiredWidth && entry.Height >= requiredHeight)
                {
                    Touch(entry);
                    owned = false;
                    return entry.Bitmap;
                }
            }
        }

        // Slow path: work out the size to decode at, then decode outside the lock so that a page's
        // images can be decoded in parallel.
        int sourceWidth;
        int sourceHeight;
        lock (_gate)
        {
            if (_index.TryGetValue(key, out var entry))
            {
                sourceWidth = entry.SourceWidth;
                sourceHeight = entry.SourceHeight;
            }
            else
            {
                var size = ImageLoader.ReadOrientedSize(path);
                sourceWidth = size.Width;
                sourceHeight = size.Height;
            }
        }

        var (requiredW, requiredH) = ImageLoader.ComputeTargetSize(sourceWidth, sourceHeight, areaWidth, areaHeight, mode);
        var (canonicalW, canonicalH) = ImageLoader.ComputeTargetSize(
            sourceWidth, sourceHeight, _canonicalBox, _canonicalBox, ZoomMode.Fit);

        var targetWidth = Math.Max(requiredW, canonicalW);
        var targetHeight = Math.Max(requiredH, canonicalH);

        var bitmap = ImageLoader.DecodeToSize(path, targetWidth, targetHeight);

        if (bitmap.ByteCount > _maxBytes)
        {
            // Too large to cache sensibly; hand it to the caller.
            owned = true;
            return bitmap;
        }

        lock (_gate)
        {
            if (_index.TryGetValue(key, out var existing))
            {
                var (existingW, existingH) = ImageLoader.ComputeTargetSize(
                    existing.SourceWidth, existing.SourceHeight, areaWidth, areaHeight, mode);

                if (existing.Width >= existingW && existing.Height >= existingH)
                {
                    // Another thread inserted a usable entry while we were decoding.
                    Touch(existing);
                    DisposeOrDefer(bitmap);
                    owned = false;
                    return existing.Bitmap;
                }

                Remove(existing);
            }

            var inserted = new Entry(key, bitmap, sourceWidth, sourceHeight);
            inserted.Node = _order.AddFirst(inserted);
            _index[key] = inserted;
            _bytes += bitmap.ByteCount;
            Evict();
        }

        owned = false;
        return bitmap;
    }

    /// <summary>Removes every cached entry and disposes the bitmaps.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            foreach (var entry in _order)
            {
                DisposeOrDefer(entry.Bitmap);
            }

            _order.Clear();
            _index.Clear();
            _bytes = 0;
        }
    }

    private void Touch(Entry entry)
    {
        _order.Remove(entry.Node);
        _order.AddFirst(entry.Node);
    }

    private void Remove(Entry entry)
    {
        _order.Remove(entry.Node);
        _index.Remove(entry.Key);
        _bytes -= entry.Bitmap.ByteCount;
        DisposeOrDefer(entry.Bitmap);
    }

    private void Evict()
    {
        // Keep at least one entry so a single oversized bitmap does not evict itself.
        while ((_index.Count > _maxEntries || _bytes > _maxBytes) && _order.Count > 1)
        {
            var last = _order.Last;
            if (last is null)
            {
                break;
            }

            Remove(last.Value);
        }
    }

    private void DisposeOrDefer(SKBitmap bitmap)
    {
        if (_pinCount > 0)
        {
            _pendingDispose.Add(bitmap);
        }
        else
        {
            bitmap.Dispose();
        }
    }

    private void Release()
    {
        lock (_gate)
        {
            _pinCount--;
            if (_pinCount > 0)
            {
                return;
            }

            foreach (var bitmap in _pendingDispose)
            {
                bitmap.Dispose();
            }

            _pendingDispose.Clear();
        }
    }

    private static string BuildKey(string path)
    {
        var stamp = 0L;
        var length = 0L;
        try
        {
            var info = new FileInfo(path);
            stamp = info.LastWriteTimeUtc.Ticks;
            length = info.Length;
        }
        catch
        {
            // If the file cannot be stat'd, fall back to the path alone.
        }

        return $"{path}|{stamp}|{length}";
    }

    private sealed class Entry(string key, SKBitmap bitmap, int sourceWidth, int sourceHeight)
    {
        public string Key { get; } = key;

        public SKBitmap Bitmap { get; } = bitmap;

        public int SourceWidth { get; } = sourceWidth;

        public int SourceHeight { get; } = sourceHeight;

        public int Width => Bitmap.Width;

        public int Height => Bitmap.Height;

        public LinkedListNode<Entry> Node { get; set; } = null!;
    }

    private sealed class PinScope(ImageCache cache) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            cache.Release();
        }
    }
}
