namespace ComiX.Archives;

/// <summary>
/// A comic archive container: an entry list and a way to read one entry.
/// Implementations must serialise concurrent calls to <see cref="CopyEntryToAsync"/>.
/// </summary>
internal interface IComicArchive : IDisposable
{
    ComicContainerFormat Format { get; }

    /// <summary>Every entry, in the order the container lists them.</summary>
    IReadOnlyList<ComicArchiveEntry> Entries { get; }

    /// <summary>Names occurring more than once. Only the first occurrence appears in <see cref="Entries"/>.</summary>
    IReadOnlyList<string> DuplicateEntryNames { get; }

    /// <summary>False for solid archives, where reaching an entry requires decompressing preceding data.</summary>
    bool SupportsRandomAccess { get; }

    /// <summary>False for one part of a multi-volume set, whose remaining content is unavailable.</summary>
    bool IsComplete { get; }

    /// <summary>The archive-level comment, where the container supports one.</summary>
    string? Comment { get; }

    /// <summary>
    /// Copies an entry's content to <paramref name="destination"/>. Size limits are applied by
    /// <see cref="Internal.ComicContentReader"/>, not by implementations of this interface.
    /// </summary>
    ValueTask CopyEntryToAsync(
        ComicArchiveEntry entry,
        Stream destination,
        CancellationToken cancellationToken);
}
