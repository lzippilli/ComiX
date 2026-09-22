using ComiX.Internal;
using SharpCompress.Archives;
using SharpCompress.Readers;
using RarFile = SharpCompress.Archives.Rar.RarArchive;
using SevenZipFile = SharpCompress.Archives.SevenZip.SevenZipArchive;
using TarFile = SharpCompress.Archives.Tar.TarArchive;
using ZipFile = SharpCompress.Archives.Zip.ZipArchive;

namespace ComiX.Archives;

/// <summary>
/// SharpCompress-backed implementation covering ZIP, RAR, 7-Zip and TAR. The formats differ only in
/// random-access support and archive comments, both parameterised here.
/// </summary>
/// <remarks>
/// <para>
/// Reads are serialised on a semaphore because the archive and its stream are shared state.
/// Serialisation is a property of this implementation, not of <see cref="IComicArchive"/>.
/// </para>
/// <para>
/// The semaphore is never disposed. <see cref="SemaphoreSlim"/> holds no unmanaged resource unless
/// its wait handle is requested, and disposing it would leave queued waiters pending indefinitely
/// and make the release in the read path throw.
/// </para>
/// </remarks>
internal sealed class SharpCompressComicArchive : IComicArchive
{
    private readonly IArchive _archive;
    private readonly Stream? _ownedStream;
    private readonly Dictionary<string, IArchiveEntry> _entriesByKey;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private int _disposed;

    private SharpCompressComicArchive(
        IArchive archive,
        ComicContainerFormat format,
        Stream? ownedStream,
        string? comment,
        bool supportsRandomAccess)
    {
        _archive = archive;
        _ownedStream = ownedStream;
        Format = format;
        Comment = comment;
        SupportsRandomAccess = supportsRandomAccess;

        var entries = new List<ComicArchiveEntry>();
        var duplicates = new List<string>();
        _entriesByKey = new Dictionary<string, IArchiveEntry>(StringComparer.Ordinal);

        foreach (var entry in archive.Entries)
        {
            if (entry.Key is not { Length: > 0 } key)
            {
                continue;
            }

            var normalisedKey = key.Replace('\\', '/');

            // Duplicate names are legal in several containers. The first occurrence is used and the
            // rest are recorded for validation.
            if (!_entriesByKey.TryAdd(normalisedKey, entry))
            {
                duplicates.Add(normalisedKey);
                continue;
            }

            entries.Add(new ComicArchiveEntry(
                normalisedKey,
                entry.Size,
                entry.IsDirectory,
                entry.IsEncrypted,
                entry.LastModifiedTime));
        }

        Entries = entries;
        DuplicateEntryNames = duplicates;
    }

    public ComicContainerFormat Format { get; }

    public IReadOnlyList<ComicArchiveEntry> Entries { get; }

    public IReadOnlyList<string> DuplicateEntryNames { get; }

    public bool SupportsRandomAccess { get; }

    public string? Comment { get; }

    public bool IsComplete => _archive.IsComplete;

    /// <summary>Opens <paramref name="stream"/> as the given container format.</summary>
    /// <param name="stream">The archive content. Must be seekable.</param>
    /// <param name="format">The format the content was detected as.</param>
    /// <param name="ownsStream">Whether disposing this archive should dispose <paramref name="stream"/>.</param>
    /// <param name="archiveOptions">The password and entry name encoding to read the archive with.</param>
    public static SharpCompressComicArchive Open(
        Stream stream,
        ComicContainerFormat format,
        bool ownsStream,
        ComicArchiveOptions archiveOptions)
    {
        var options = new ReaderOptions { LeaveStreamOpen = true, Password = archiveOptions.Password };

        if (archiveOptions.EntryNameEncoding is { } encoding)
        {
            // Default, not Forced: entries that declare UTF-8 must keep being decoded as UTF-8.
            options.ArchiveEncoding.Default = encoding;
        }

        // Read before opening: the comment is located at the end of the file and reading it moves
        // the stream position.
        var zipComment = format is ComicContainerFormat.Cbz ? ZipArchiveComment.Read(stream) : null;

        try
        {
            return format switch
            {
                ComicContainerFormat.Cbz => Create(ZipFile.Open(stream, options), zipComment),
                ComicContainerFormat.Cbr => Create(RarFile.Open(stream, options)),
                ComicContainerFormat.Cb7 => Create(SevenZipFile.Open(stream, options)),
                ComicContainerFormat.Cbt => Create(TarFile.Open(stream, options)),
                _ => throw new UnsupportedComicFormatException(
                    "The file is not a comic archive in a recognised container format.",
                    format),
            };
        }
        catch (Exception ex) when (ex is not ComiXException and not OperationCanceledException)
        {
            throw new ComicArchiveException($"The {format} container could not be read.", ex);
        }

        SharpCompressComicArchive Create(IArchive archive, string? comment = null) =>
            new(
                archive,
                format,
                ownsStream ? stream : null,
                comment ?? CommentOf(archive),
                SupportsRandomAccessIn(archive));
    }

    private bool IsDisposed => Volatile.Read(ref _disposed) == 1;

    public async ValueTask CopyEntryToAsync(
        ComicArchiveEntry entry,
        Stream destination,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, typeof(ComicBook));

        if (!_entriesByKey.TryGetValue(entry.Key, out var archiveEntry))
        {
            throw new ComicArchiveException($"Entry '{entry.Key}' is no longer present in the archive.", entry.Key);
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Disposal may have started while this call was queued. Checked outside the wrapping
            // block below so the exception reaches the caller as ObjectDisposedException.
            ObjectDisposedException.ThrowIf(IsDisposed, typeof(ComicBook));

            try
            {
                Stream source;
                try
                {
                    source = await archiveEntry.OpenEntryStreamAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not ComiXException and not OperationCanceledException)
                {
                    throw new ComicArchiveException($"Entry '{entry.Key}' could not be opened.", ex);
                }

                await using (source.ConfigureAwait(false))
                {
                    await source.CopyToAsync(destination, StreamCopy.BufferSize, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            catch (Exception ex) when (ex is not ComiXException and not OperationCanceledException)
            {
                throw new ComicArchiveException($"Entry '{entry.Key}' could not be read.", ex);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Marks the archive disposed without waiting. Resources are released immediately when no read
    /// is in progress; otherwise once that read completes.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        if (_gate.Wait(0))
        {
            try
            {
                ReleaseResources();
            }
            finally
            {
                _gate.Release();
            }
        }
        else
        {
            _ = ReleaseWhenIdleAsync();
        }
    }

    /// <summary>
    /// Marks the archive disposed, so that no further read starts, then waits for the read in
    /// progress to finish before releasing resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        await ReleaseWhenIdleAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Acquires the gate, which completes only after the read in progress has finished, and
    /// releases resources under it. Queued reads that acquire the gate first observe the disposed
    /// flag and throw without touching the archive.
    /// </summary>
    private async Task ReleaseWhenIdleAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            ReleaseResources();
        }
        finally
        {
            _gate.Release();
        }
    }

    private void ReleaseResources()
    {
        _archive.Dispose();
        _ownedStream?.Dispose();
    }

    /// <summary>
    /// Only ZIP and RAR define an archive-level comment. ZIP comments are read by
    /// <see cref="ZipArchiveComment"/>, since SharpCompress does not expose them.
    /// </summary>
    private static string? CommentOf(IArchive archive) =>
        archive switch
        {
            RarFile rar => rar.Volumes.FirstOrDefault()?.Comment,
            _ => null,
        };

    /// <summary>
    /// Solid archives store entries in shared compression blocks: reaching an entry requires
    /// decompressing preceding data.
    /// </summary>
    private static bool SupportsRandomAccessIn(IArchive archive) => !archive.IsSolid;
}
