using SharpCompress.Archives;

namespace ComiX.Archives;

/// <summary>
/// The entries of an open archive, normalised into the ComiX model and indexed by key.
/// </summary>
internal sealed class ArchiveIndex
{
    private readonly Dictionary<string, IArchiveEntry> _byKey;

    private ArchiveIndex(
        Dictionary<string, IArchiveEntry> byKey,
        IReadOnlyList<ComicArchiveEntry> entries,
        IReadOnlyList<string> duplicateEntryNames)
    {
        _byKey = byKey;
        Entries = entries;
        DuplicateEntryNames = duplicateEntryNames;
    }

    /// <summary>Every entry, in the order the container lists them.</summary>
    public IReadOnlyList<ComicArchiveEntry> Entries { get; }

    /// <summary>Names occurring more than once. Only the first occurrence appears in <see cref="Entries"/>.</summary>
    public IReadOnlyList<string> DuplicateEntryNames { get; }

    /// <summary>The underlying archive entries, keyed by normalised key.</summary>
    public IReadOnlyDictionary<string, IArchiveEntry> ByKey => _byKey;

    /// <summary>How many entries hold content, which is how many a traversal returns.</summary>
    public int ContentEntryCount { get; private init; }

    /// <summary>Converts a container's entry path to the form used throughout ComiX.</summary>
    public static string Normalise(string key) => key.Replace('\\', '/');

    /// <summary>Indexes the entries of <paramref name="archive"/>.</summary>
    public static ArchiveIndex Build(IArchive archive)
    {
        var byKey = new Dictionary<string, IArchiveEntry>(StringComparer.Ordinal);
        var entries = new List<ComicArchiveEntry>();
        var duplicates = new List<string>();

        foreach (var entry in archive.Entries)
        {
            if (entry.Key is not { Length: > 0 } key)
            {
                continue;
            }

            var normalisedKey = Normalise(key);

            // Duplicate names are legal in several containers. The first occurrence is used and the
            // rest are recorded for validation.
            if (!byKey.TryAdd(normalisedKey, entry))
            {
                duplicates.Add(normalisedKey);
                continue;
            }

            entries.Add(new ComicArchiveEntry(
                normalisedKey,
                entry.Size,
                entry.IsDirectory,
                entry.IsEncrypted,
                entry.LastModifiedTime)
            {
                Crc = entry.Crc,
            });
        }

        return new ArchiveIndex(byKey, entries, duplicates)
        {
            ContentEntryCount = entries.Count(entry => !entry.IsDirectory),
        };
    }
}
