using SharpCompress.Archives;
using RarFile = SharpCompress.Archives.Rar.RarArchive;

namespace ComiX.Archives;

/// <summary>
/// SharpCompress-backed implementation covering ZIP, RAR, 7-Zip and TAR.
/// </summary>
/// <remarks>
/// Reads are serialised on a semaphore because the archive and its stream are shared state.
/// Serialisation is a property of this implementation, not of <see cref="IComicArchive"/>.
/// </remarks>
internal sealed class SharpCompressComicArchive : IComicArchive
{
    private readonly IArchive _archive;
    private readonly Stream? _ownedStream;
    private readonly IEntryAccess _entryAccess;

    // Never disposed: SemaphoreSlim holds no unmanaged resource unless its wait handle is requested,
    // and disposing it would strand queued waiters and make the release in the read path throw.
    private readonly SemaphoreSlim _gate = new(1, 1);

    private int _disposed;

    private SharpCompressComicArchive(
        ComicContainerFormat format,
        IArchive archive,
        Stream? ownedStream,
        string? comment,
        ArchiveIndex index,
        IEntryAccess entryAccess)
    {
        _archive = archive;
        _ownedStream = ownedStream;
        _entryAccess = entryAccess;

        Format = format;
        Comment = comment;
        Entries = index.Entries;
        DuplicateEntryNames = index.DuplicateEntryNames;
        SupportsRandomAccess = !archive.IsSolid;
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
    /// <param name="archiveOptions">The settings to read the archive with.</param>
    public static SharpCompressComicArchive Open(
        Stream stream,
        ComicContainerFormat format,
        bool ownsStream,
        ComicArchiveOptions archiveOptions)
    {
        var opener = new SharpCompressArchiveOpener(format, archiveOptions);

        // Read before opening: the comment is located at the end of the file and reading it moves the
        // stream position.
        var zipComment = format is ComicContainerFormat.Cbz ? ZipArchiveComment.Read(stream) : null;

        try
        {
            // Headers and entries are read lazily, so opening, indexing and inspecting the archive can
            // each be where a malformed container first fails. All three report it the same way.
            var archive = opener.Open(stream);
            var index = ArchiveIndex.Build(archive);
            var entryAccess = EntryAccess.For(
                archive,
                index,
                () => opener.Reopen(stream),
                archiveOptions.MaxEntrySizeInBytes);

            return new SharpCompressComicArchive(
                format,
                archive,
                ownsStream ? stream : null,
                zipComment ?? CommentOf(archive),
                index,
                entryAccess);
        }
        catch (Exception ex) when (ex is not ComiXException and not OperationCanceledException)
        {
            throw new ComicArchiveException($"The {format} container could not be read.", ex);
        }
    }

    private bool IsDisposed => Volatile.Read(ref _disposed) == 1;

    public async ValueTask CopyEntryToAsync(
        ComicArchiveEntry entry,
        Stream destination,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, typeof(ComicBook));

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Disposal may have started while this call was queued. Checked outside the wrapping
            // block below so the exception reaches the caller as ObjectDisposedException.
            ObjectDisposedException.ThrowIf(IsDisposed, typeof(ComicBook));

            try
            {
                await _entryAccess.CopyEntryToAsync(entry, destination, cancellationToken).ConfigureAwait(false);
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
        // The strategy first: it may hold an archive of its own, opened for a sequential traversal.
        _entryAccess.Dispose();
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
}
