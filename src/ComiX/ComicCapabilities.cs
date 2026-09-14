namespace ComiX;

/// <summary>
/// The operations supported by an open <see cref="ComicBook"/>. Capabilities vary by container
/// format, allowing callers to branch on these flags rather than on <see cref="ComicContainerFormat"/>.
/// </summary>
[Flags]
public enum ComicCapabilities
{
    /// <summary>No capabilities.</summary>
    None = 0,

    /// <summary>Page and resource content can be read.</summary>
    ReadContent = 1,

    /// <summary>Entries can be read in any order without re-reading the whole container.</summary>
    RandomAccess = 1 << 1,

    /// <summary>Embedded metadata can be read.</summary>
    ReadMetadata = 1 << 2,

    /// <summary>The comic can be exported to another container via <see cref="ComicBook.ExportAsync"/>.</summary>
    Export = 1 << 3,
}
