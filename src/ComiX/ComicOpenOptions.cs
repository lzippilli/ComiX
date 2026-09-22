using System.Text;

namespace ComiX;

/// <summary>
/// Options controlling how a comic is opened and the resource limits applied while reading it.
/// </summary>
/// <remarks>
/// Archives are treated as untrusted input. The default limits bound the cost of a malformed or
/// hostile archive and should be raised only for trusted sources.
/// </remarks>
public sealed record ComicOpenOptions
{
    /// <summary>The options used when none are supplied.</summary>
    public static ComicOpenOptions Default { get; } = new();

    /// <summary>
    /// Reads embedded metadata while opening. When <see langword="false"/>,
    /// <see cref="ComicBook.Metadata"/> is <see cref="ComicMetadata.Empty"/> and no metadata is parsed.
    /// </summary>
    public bool ReadMetadata { get; init; } = true;

    /// <summary>Where extracted page content is cached. Defaults to <see cref="ComicCache.None"/>.</summary>
    public ComicCache Cache { get; init; } = ComicCache.None;

    /// <summary>The password for archives whose entries are encrypted.</summary>
    public string? Password { get; init; }

    /// <summary>
    /// The encoding assumed for entry names that the archive does not declare as UTF-8. Defaults to
    /// <see langword="null"/>, which decodes them as UTF-8.
    /// </summary>
    /// <remarks>
    /// Entry names are ASCII in most archives, and ASCII decodes identically under every relevant
    /// encoding. Set this only for archives whose non-ASCII names were written in a legacy code page,
    /// which <see cref="ComicValidator"/> reports as
    /// <see cref="ComicValidationCode.EntryNameEncodingSuspect"/>. Legacy code pages require the
    /// consumer to register <c>CodePagesEncodingProvider</c>.
    /// </remarks>
    public Encoding? EntryNameEncoding { get; init; }

    /// <summary>
    /// The maximum content read from a single entry, 256 MiB by default. Exceeding it raises a
    /// <see cref="ComicArchiveException"/>. Evaluated against bytes read, not against declared sizes.
    /// </summary>
    public long MaxEntrySizeInBytes { get; init; } = 256L * 1024 * 1024;

    /// <summary>
    /// The maximum number of entries an archive may contain, 50,000 by default. Checked before any
    /// content is read.
    /// </summary>
    public int MaxEntryCount { get; init; } = 50_000;
}
