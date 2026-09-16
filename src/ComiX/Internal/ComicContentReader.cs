using ComiX.Archives;
using ComiX.Cache;

namespace ComiX.Internal;

/// <summary>
/// Reads entry content for pages and resources, applying the cache and the entry size limit.
/// </summary>
/// <remarks>
/// All content reads pass through this type, which wraps the destination in a
/// <see cref="LimitedStream"/>; archive implementations therefore do not enforce limits themselves.
/// <see cref="OpenAsync"/> buffers a single entry, which makes the returned stream independent of the
/// archive's lifetime. <see cref="CopyToAsync"/> writes directly to the destination without buffering.
/// </remarks>
internal sealed class ComicContentReader
{
    private const int InitialBufferCap = 1024 * 1024;

    private readonly IComicArchive _archive;
    private readonly IComicCache _cache;
    private readonly string _cacheScope;
    private readonly long _maxEntrySize;
    private volatile bool _disposed;

    public ComicContentReader(IComicArchive archive, IComicCache cache, string cacheScope, long maxEntrySize)
    {
        _archive = archive;
        _cache = cache;
        _cacheScope = cacheScope;
        _maxEntrySize = maxEntrySize;
    }

    public async Task<Stream> OpenAsync(ComicArchiveEntry entry, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, typeof(ComicBook));

        var content = await ReadAllAsync(entry, cancellationToken).ConfigureAwait(false);
        return new MemoryStream(content, writable: false);
    }

    public async Task CopyToAsync(ComicArchiveEntry entry, Stream destination, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, typeof(ComicBook));

        var cached = await _cache.TryGetAsync(_cacheScope, entry.Key, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            await destination.WriteAsync(cached, cancellationToken).ConfigureAwait(false);
            return;
        }

        var limited = new LimitedStream(destination, _maxEntrySize, entry.Key);
        await _archive.CopyEntryToAsync(entry, limited, cancellationToken).ConfigureAwait(false);
    }

    public async Task ExtractToFileAsync(
        ComicArchiveEntry entry,
        string destinationPath,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, typeof(ComicBook));

        var fullPath = Path.GetFullPath(destinationPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var mode = overwrite ? FileMode.Create : FileMode.CreateNew;
        var destination = new FileStream(
            fullPath,
            mode,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.Asynchronous);

        await using (destination.ConfigureAwait(false))
        {
            await CopyToAsync(entry, destination, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Rejects further reads. Called by <see cref="ComicBook"/> on disposal.</summary>
    public void MarkDisposed() => _disposed = true;

    private async Task<byte[]> ReadAllAsync(ComicArchiveEntry entry, CancellationToken cancellationToken)
    {
        var cached = await _cache.TryGetAsync(_cacheScope, entry.Key, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            return cached;
        }

        // The declared size only sizes the initial buffer; the real limit is enforced while copying.
        var capacity = (int)Math.Clamp(entry.Size, 0, InitialBufferCap);
        using var buffer = new MemoryStream(capacity);
        await CopyToAsync(entry, buffer, cancellationToken).ConfigureAwait(false);

        var content = buffer.ToArray();
        await _cache.SetAsync(_cacheScope, entry.Key, content, cancellationToken).ConfigureAwait(false);
        return content;
    }
}
