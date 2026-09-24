using System.Globalization;
using ComiX.Archives;
using ComiX.Conversion;
using ComiX.Detection;
using ComiX.Internal;
using ComiX.Metadata;

namespace ComiX;

/// <summary>
/// A comic, independent of the container format it is stored in. Primary entry point of the library.
/// </summary>
/// <example>
/// <code>
/// await using var comic = await ComicBook.OpenAsync("example.cbz");
///
/// Console.WriteLine(comic.Metadata.Title);
/// Console.WriteLine(comic.Pages.Count);
///
/// await using var page = await comic.Pages[0].OpenAsync();
/// </code>
/// </example>
/// <remarks>
/// Opening reads the container index and resolves metadata; page content is accessed lazily.
/// Instances may be accessed concurrently: content reads are serialised internally and parallel
/// archive I/O is not guaranteed. Disposal lets the read in progress complete —
/// <see cref="DisposeAsync"/> waits for it, <see cref="Dispose"/> does not — and reads requested
/// after either call throw <see cref="ObjectDisposedException"/>.
/// </remarks>
public sealed class ComicBook : IDisposable, IAsyncDisposable
{
    private readonly IComicArchive _archive;
    private readonly ComicContentReader _reader;
    private readonly IReadOnlyList<ComicArchiveEntry> _pageEntries;
    private readonly IReadOnlyList<ComicArchiveEntry> _resourceEntries;
    private bool _disposed;

    private ComicBook(
        IComicArchive archive,
        ComicContentReader reader,
        string? filePath,
        ComicMetadata metadata,
        IReadOnlyList<ComicArchiveEntry> pageEntries,
        IReadOnlyList<ComicArchiveEntry> resourceEntries,
        IReadOnlyList<PageMetadata> pageMetadata)
    {
        _archive = archive;
        _reader = reader;
        _pageEntries = pageEntries;
        _resourceEntries = resourceEntries;

        FilePath = filePath;
        Metadata = metadata;

        var matchedMetadata = PageMetadataMatcher.Match(pageEntries, pageMetadata);
        Pages =
        [
            .. pageEntries.Select((entry, index) => new ComicPage(reader, entry, index, matchedMetadata[index])),
        ];

        Resources = [.. resourceEntries.Select(entry => new ComicResource(reader, entry))];
    }

    /// <summary>The container format determined from the archive contents.</summary>
    public ComicContainerFormat Format => _archive.Format;

    /// <summary>The file the comic was opened from, or <see langword="null"/> when opened from a stream.</summary>
    public string? FilePath { get; }

    /// <summary>The operations supported for this comic, which vary by container format.</summary>
    public ComicCapabilities Capabilities =>
        ComicCapabilities.ReadContent
        | ComicCapabilities.ReadMetadata
        | ComicCapabilities.Export
        | (_archive.SupportsRandomAccess ? ComicCapabilities.RandomAccess : ComicCapabilities.None);

    /// <summary>
    /// Metadata normalised from the resolved source. Canonical values are empty when no source could
    /// be parsed; located sources remain listed in <see cref="ComicMetadata.Sources"/>.
    /// </summary>
    public ComicMetadata Metadata { get; }

    /// <summary>The archive's images, in reading order.</summary>
    public IReadOnlyList<ComicPage> Pages { get; }

    /// <summary>The archive's non-image entries, including metadata files.</summary>
    public IReadOnlyList<ComicResource> Resources { get; }

    /// <summary>Opens the comic archive at <paramref name="path"/>.</summary>
    /// <param name="path">The archive to open. The format is determined from the contents, not the extension.</param>
    /// <param name="options">Reading options, or <see langword="null"/> for <see cref="ComicOpenOptions.Default"/>.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>An open <see cref="ComicBook"/>, which the caller must dispose.</returns>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="UnsupportedComicFormatException">The container format is not supported.</exception>
    /// <exception cref="ComicArchiveException">The archive is malformed, or exceeds a configured limit.</exception>
    public static async Task<ComicBook> OpenAsync(
        string path,
        ComicOpenOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var file = new FileInfo(path);
        if (!file.Exists)
        {
            throw new FileNotFoundException("The comic archive was not found.", path);
        }

        var stream = new FileStream(
            file.FullName,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.RandomAccess);

        try
        {
            return await OpenCoreAsync(
                    stream,
                    ownsStream: true,
                    filePath: file.FullName,
                    cacheScope: CacheScopeFor(file),
                    options ?? ComicOpenOptions.Default,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            await stream.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Opens a comic archive from a stream.</summary>
    /// <param name="stream">
    /// The archive content. Must be readable and seekable, and must not be read or repositioned by
    /// the caller while the comic is open: reading a solid archive repositions the stream.
    /// </param>
    /// <param name="options">Reading options, or <see langword="null"/> for <see cref="ComicOpenOptions.Default"/>.</param>
    /// <param name="leaveOpen">
    /// When <see langword="true"/>, the default, the stream is not disposed with this instance and
    /// must remain open for its lifetime.
    /// </param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>An open <see cref="ComicBook"/>, which the caller must dispose.</returns>
    /// <exception cref="ArgumentException">The stream is not readable or not seekable.</exception>
    /// <exception cref="UnsupportedComicFormatException">The container format is not supported.</exception>
    /// <exception cref="ComicArchiveException">The archive is malformed, or exceeds a configured limit.</exception>
    public static Task<ComicBook> OpenAsync(
        Stream stream,
        ComicOpenOptions? options = null,
        bool leaveOpen = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanRead)
        {
            throw new ArgumentException("The stream must be readable.", nameof(stream));
        }

        if (!stream.CanSeek)
        {
            throw new ArgumentException(
                "The stream must be seekable. Copy the content to a seekable stream, or open the comic from a file path.",
                nameof(stream));
        }

        return OpenCoreAsync(
            stream,
            ownsStream: !leaveOpen,
            filePath: null,
            cacheScope: Guid.NewGuid().ToString("N"),
            options ?? ComicOpenOptions.Default,
            cancellationToken);
    }

    /// <summary>
    /// Extracts pages and, optionally, resources into <paramref name="destinationDirectory"/>. Entry
    /// names are sanitised and each resolved destination is verified to be contained within it.
    /// </summary>
    /// <param name="destinationDirectory">The target directory. Created if it does not exist.</param>
    /// <param name="options">Extraction options, or <see langword="null"/> for <see cref="ComicExtractionOptions.Default"/>.</param>
    /// <param name="cancellationToken">Cancels the operation. Files already written are retained.</param>
    /// <exception cref="IOException">A destination file exists and overwriting is not enabled.</exception>
    /// <exception cref="ComicArchiveException">An entry cannot be read, or resolves outside the destination directory.</exception>
    public async Task ExtractAllAsync(
        string destinationDirectory,
        ComicExtractionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);

        options ??= ComicExtractionOptions.Default;
        Directory.CreateDirectory(destinationDirectory);

        var entries = options.IncludeResources ? _pageEntries.Concat(_resourceEntries) : _pageEntries;

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var destination = PathSafety.ResolveDestination(destinationDirectory, entry.Key, options.Flatten);
            await _reader.ExtractToFileAsync(entry, destination, options.Overwrite, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>Writes the comic to <paramref name="destinationPath"/> in another container format.</summary>
    /// <remarks>
    /// Page content is copied without re-encoding, entry names and order are preserved, and the
    /// metadata file is copied verbatim unless <see cref="ComicExportOptions.IncludeResources"/> is
    /// disabled. Entries are processed individually; memory consumption does not scale with archive size.
    /// </remarks>
    /// <param name="destinationPath">The archive to write.</param>
    /// <param name="format">The target container format. Only <see cref="ComicExportFormat.Cbz"/> is supported.</param>
    /// <param name="options">Export options, or <see langword="null"/> for <see cref="ComicExportOptions.Default"/>.</param>
    /// <param name="cancellationToken">Cancels the operation. A partially written archive is deleted.</param>
    /// <exception cref="ComiXException">The destination is the file the comic was opened from.</exception>
    /// <exception cref="IOException">The destination exists and <see cref="ComicExportOptions.Overwrite"/> is <see langword="false"/>.</exception>
    public async Task ExportAsync(
        string destinationPath,
        ComicExportFormat format = ComicExportFormat.Cbz,
        ComicExportOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        if (format != ComicExportFormat.Cbz)
        {
            throw new UnsupportedComicFormatException(
                $"Exporting to {format} is not supported.",
                ComicContainerFormat.Unknown);
        }

        var fullDestination = Path.GetFullPath(destinationPath);
        if (FilePath is not null
            && string.Equals(fullDestination, FilePath, StringComparison.OrdinalIgnoreCase))
        {
            throw new ComiXException("A comic cannot be exported over the file it was opened from.");
        }

        options ??= ComicExportOptions.Default;
        IReadOnlyList<ComicArchiveEntry> entries = options.IncludeResources
            ? [.. _pageEntries, .. _resourceEntries]
            : _pageEntries;

        await CbzExporter.ExportAsync(
                entries,
                _reader,
                _archive.Comment,
                fullDestination,
                options.Overwrite,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Marks the comic disposed and returns immediately. The archive and, when owned by this
    /// instance, the underlying stream are released once the read in progress, if any, completes.
    /// </summary>
    /// <remarks>
    /// Reads requested afterwards throw <see cref="ObjectDisposedException"/>. Streams already
    /// returned by <see cref="ComicPage.OpenAsync"/> remain valid. Use <see cref="DisposeAsync"/> to
    /// observe the release.
    /// </remarks>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _reader.MarkDisposed();
        _archive.Dispose();
    }

    /// <summary>
    /// Releases the archive and, when owned by this instance, the underlying stream, after the read
    /// in progress has finished.
    /// </summary>
    /// <remarks>
    /// Reads requested after this call, including reads already queued, throw
    /// <see cref="ObjectDisposedException"/>. Streams already returned by
    /// <see cref="ComicPage.OpenAsync"/> remain valid.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _reader.MarkDisposed();
        await _archive.DisposeAsync().ConfigureAwait(false);
    }

    private static async Task<ComicBook> OpenCoreAsync(
        Stream stream,
        bool ownsStream,
        string? filePath,
        string cacheScope,
        ComicOpenOptions options,
        CancellationToken cancellationToken)
    {
        var header = await HeaderReader.ReadAsync(stream, cancellationToken).ConfigureAwait(false);
        var format = ContainerSignature.Identify(header);

        var archive = ComicArchiveFactory.Open(
            stream,
            format,
            ownsStream,
            new ComicArchiveOptions(
                options.Password,
                options.EntryNameEncoding,
                SourcePath: filePath,
                options.MaxEntrySizeInBytes));
        try
        {
            if (archive.Entries.Count > options.MaxEntryCount)
            {
                throw new ComicArchiveException(
                    $"The archive declares {archive.Entries.Count} entries, more than the configured limit of {options.MaxEntryCount}.");
            }

            var pageEntries = ArchiveContent.SelectPages(archive.Entries);
            var resourceEntries = ArchiveContent.SelectResources(archive.Entries);
            var reader = new ComicContentReader(
                archive,
                options.Cache.Implementation,
                cacheScope,
                options.MaxEntrySizeInBytes);

            var metadata = ComicMetadata.Empty;
            IReadOnlyList<PageMetadata> pageMetadata = [];
            if (options.ReadMetadata)
            {
                var resolved = await MetadataResolver
                    .ResolveAsync(archive, reader, cancellationToken)
                    .ConfigureAwait(false);
                metadata = resolved.Document.Metadata;
                pageMetadata = resolved.Document.Pages;
            }

            return new ComicBook(
                archive,
                reader,
                filePath,
                metadata,
                pageEntries,
                resourceEntries,
                pageMetadata);
        }
        catch
        {
            archive.Dispose();
            throw;
        }
    }

    private static string CacheScopeFor(FileInfo file) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{file.FullName}|{file.Length}|{file.LastWriteTimeUtc.Ticks}");
}
