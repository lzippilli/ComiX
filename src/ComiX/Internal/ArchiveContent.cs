using ComiX.Archives;

namespace ComiX.Internal;

/// <summary>
/// Classifies archive entries into pages, resources and metadata, and defines the page order.
/// </summary>
/// <remarks>
/// <para>Selection and ordering rules, applied in this order:</para>
/// <list type="number">
///   <item>Directories and archiver artefacts (<c>__MACOSX</c>, <c>.DS_Store</c>, resource forks,
///   <c>Thumbs.db</c>, <c>desktop.ini</c>) are excluded.</item>
///   <item>An entry is a page when its extension denotes a supported image format; all other entries
///   are resources.</item>
///   <item>Pages are ordered by directory path, then by file name, both compared numerically for
///   embedded digit runs (<c>2.jpg</c> before <c>10.jpg</c>).</item>
///   <item>An entry at the archive root whose name without extension is <c>cover</c> is ordered
///   first.</item>
///   <item>Remaining ties are resolved by ordinal comparison, making the order independent of the
///   container's entry order.</item>
/// </list>
/// </remarks>
internal static class ArchiveContent
{
    private const string ComicInfoFileName = "ComicInfo.xml";
    private const string CoMetFileName = "comet.xml";
    private const string MetronInfoFileName = "MetronInfo.xml";

    /// <summary>The file names ComiX recognises as metadata rather than as content.</summary>
    public static bool IsMetadataFile(ComicArchiveEntry entry) =>
        entry.FileName.Equals(ComicInfoFileName, StringComparison.OrdinalIgnoreCase)
        || entry.FileName.Equals(CoMetFileName, StringComparison.OrdinalIgnoreCase)
        || entry.FileName.Equals(MetronInfoFileName, StringComparison.OrdinalIgnoreCase);

    public static bool IsJunk(ComicArchiveEntry entry)
    {
        if (entry.Key.StartsWith("__MACOSX/", StringComparison.OrdinalIgnoreCase)
            || entry.Key.Contains("/__MACOSX/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var fileName = entry.FileName;
        return fileName.StartsWith("._", StringComparison.Ordinal)
            || fileName.Equals(".DS_Store", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("Thumbs.db", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsContent(ComicArchiveEntry entry) =>
        !entry.IsDirectory && !IsJunk(entry) && entry.FileName.Length > 0;

    public static List<ComicArchiveEntry> SelectPages(IEnumerable<ComicArchiveEntry> entries)
    {
        var pages = entries
            .Where(entry => IsContent(entry) && ImageFormats.FromFileName(entry.FileName) != ComicImageFormat.Unknown)
            .ToList();

        pages.Sort(ComparePages);
        return pages;
    }

    public static List<ComicArchiveEntry> SelectResources(IEnumerable<ComicArchiveEntry> entries)
    {
        var resources = entries
            .Where(entry => IsContent(entry) && ImageFormats.FromFileName(entry.FileName) == ComicImageFormat.Unknown)
            .ToList();

        resources.Sort(ComparePages);
        return resources;
    }

    /// <summary>
    /// Finds the archive's <c>ComicInfo.xml</c>, preferring one at the archive root.
    /// </summary>
    public static ComicArchiveEntry? FindComicInfo(IEnumerable<ComicArchiveEntry> entries) =>
        FindByName(entries, ComicInfoFileName);

    /// <summary>
    /// Finds the archive's CoMet document, preferring one at the archive root. Located by file name,
    /// as the standard specifies, rather than by parsing every XML entry.
    /// </summary>
    public static ComicArchiveEntry? FindCoMet(IEnumerable<ComicArchiveEntry> entries) =>
        FindByName(entries, CoMetFileName);

    /// <summary>Finds the archive's <c>MetronInfo.xml</c>, preferring one at the archive root.</summary>
    public static ComicArchiveEntry? FindMetronInfo(IEnumerable<ComicArchiveEntry> entries) =>
        FindByName(entries, MetronInfoFileName);

    private static ComicArchiveEntry? FindByName(IEnumerable<ComicArchiveEntry> entries, string fileName)
    {
        ComicArchiveEntry? nested = null;
        foreach (var entry in entries)
        {
            if (entry.IsDirectory || !entry.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (entry.DirectoryName.Length == 0)
            {
                return entry;
            }

            nested ??= entry;
        }

        return nested;
    }

    private static int ComparePages(ComicArchiveEntry left, ComicArchiveEntry right)
    {
        var coverComparison = IsRootCover(right).CompareTo(IsRootCover(left));
        if (coverComparison != 0)
        {
            return coverComparison;
        }

        var directoryComparison = NaturalComparer.Instance.Compare(left.DirectoryName, right.DirectoryName);
        if (directoryComparison != 0)
        {
            return directoryComparison;
        }

        var fileComparison = NaturalComparer.Instance.Compare(left.FileName, right.FileName);
        return fileComparison != 0 ? fileComparison : string.CompareOrdinal(left.Key, right.Key);
    }

    private static bool IsRootCover(ComicArchiveEntry entry) =>
        entry.DirectoryName.Length == 0
        && Path.GetFileNameWithoutExtension(entry.FileName).Equals("cover", StringComparison.OrdinalIgnoreCase);
}
