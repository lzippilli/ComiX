using ComiX.Archives;
using ComiX.Internal;

namespace ComiX;

/// <summary>
/// A single page of a comic. Content is accessed lazily.
/// </summary>
/// <remarks>
/// Instances may be read concurrently, including pages of the same <see cref="ComicBook"/>. Reads are
/// serialised internally; parallel archive I/O is not guaranteed.
/// </remarks>
public sealed class ComicPage
{
    private readonly ComicContentReader _reader;
    private readonly ComicArchiveEntry _entry;

    internal ComicPage(
        ComicContentReader reader,
        ComicArchiveEntry entry,
        int index,
        Metadata.PageMetadata? metadata)
    {
        _reader = reader;
        _entry = entry;
        Index = index;
        ImageFormat = ImageFormats.FromFileName(entry.FileName);
        MimeType = ImageFormats.MimeTypeOf(ImageFormat);
        Type = metadata?.Type ?? ComicPageType.Unknown;
        DeclaredSpread = metadata?.Spread ?? ComicPageSpread.Unknown;
        DeclaredWidth = metadata?.Width;
        DeclaredHeight = metadata?.Height;
        Bookmark = metadata?.Bookmark;
    }

    /// <summary>The zero-based position of this page in <see cref="ComicBook.Pages"/>.</summary>
    public int Index { get; }

    /// <summary>The archive entry path, using <c>/</c> as separator.</summary>
    public string Name => _entry.Key;

    /// <summary>The entry file name, without its directory path.</summary>
    public string FileName => _entry.FileName;

    /// <summary>The uncompressed size declared by the archive, in bytes. Declared sizes are not verified.</summary>
    public long SizeInBytes => _entry.Size;

    /// <summary>The image format implied by the file extension.</summary>
    public ComicImageFormat ImageFormat { get; }

    /// <summary>The MIME type corresponding to <see cref="ImageFormat"/>, or <see langword="null"/> if unrecognised.</summary>
    public string? MimeType { get; }

    /// <summary>
    /// The width in pixels declared by the source metadata, or <see langword="null"/> if not declared.
    /// Not verified against the image, which is never decoded.
    /// </summary>
    public int? DeclaredWidth { get; }

    /// <summary>The height in pixels declared by the source metadata, or <see langword="null"/> if not declared.</summary>
    public int? DeclaredHeight { get; }

    /// <summary>The editorial page type declared by the source metadata.</summary>
    public ComicPageType Type { get; }

    /// <summary>
    /// The spread type declared by the source metadata. <see cref="ComicPageSpread.Unknown"/> when no
    /// source declares it, which includes every standard other than ComicInfo.
    /// </summary>
    public ComicPageSpread DeclaredSpread { get; }

    /// <summary>The bookmark label declared by the source metadata, if any.</summary>
    public string? Bookmark { get; }

    /// <summary>Reads the page and returns a seekable, read-only stream over its content.</summary>
    /// <remarks>
    /// The stream is owned by the caller, is independent of the originating <see cref="ComicBook"/>,
    /// and remains readable after that instance is disposed. The page is buffered in memory; use
    /// <see cref="ExtractAsync(Stream, CancellationToken)"/> to avoid buffering.
    /// </remarks>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A seekable, read-only stream over the page content.</returns>
    /// <exception cref="ComicArchiveException">The page cannot be read, or exceeds the configured entry size limit.</exception>
    public Task<Stream> OpenAsync(CancellationToken cancellationToken = default) =>
        _reader.OpenAsync(_entry, cancellationToken);

    /// <summary>Writes the page content to <paramref name="destination"/> without buffering it in memory.</summary>
    /// <param name="destination">The target stream. It is not disposed.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <exception cref="ComicArchiveException">The page cannot be read, or exceeds the configured entry size limit.</exception>
    public Task ExtractAsync(Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return _reader.CopyToAsync(_entry, destination, cancellationToken);
    }

    /// <summary>Writes the page content to a file, creating missing directories in the destination path.</summary>
    /// <param name="destinationPath">The file to write.</param>
    /// <param name="overwrite">Replaces an existing file when <see langword="true"/>.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <exception cref="IOException">The file exists and <paramref name="overwrite"/> is <see langword="false"/>.</exception>
    /// <exception cref="ComicArchiveException">The page cannot be read, or exceeds the configured entry size limit.</exception>
    public Task ExtractAsync(
        string destinationPath,
        bool overwrite = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        return _reader.ExtractToFileAsync(_entry, destinationPath, overwrite, cancellationToken);
    }

    /// <inheritdoc />
    public override string ToString() => $"[{Index}] {Name}";
}
