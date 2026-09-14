using ComiX.Archives;
using ComiX.Metadata;

namespace ComiX.Internal;

/// <summary>
/// Attaches per-page metadata to the pages it describes.
/// </summary>
/// <remarks>
/// ComicInfo identifies pages positionally (<c>Image="3"</c>), which depends on the reader ordering
/// pages as the tagging application did. When every <c>Page</c> element carries a <c>Key</c> that
/// resolves to a distinct page, matching is performed by name instead. The strategy is applied to all
/// entries or to none: mixing name and positional matching can misattribute the remaining entries.
/// </remarks>
internal static class PageMetadataMatcher
{
    /// <summary>
    /// Returns metadata for each page, aligned with <paramref name="pages"/>, with
    /// <see langword="null"/> where a page has none.
    /// </summary>
    public static PageMetadata?[] Match(
        IReadOnlyList<ComicArchiveEntry> pages,
        IReadOnlyList<PageMetadata> metadata)
    {
        var matched = new PageMetadata?[pages.Count];
        if (metadata.Count == 0)
        {
            return matched;
        }

        if (TryMatchByKey(pages, metadata, matched))
        {
            return matched;
        }

        var byIndex = new Dictionary<int, PageMetadata>(metadata.Count);
        foreach (var page in metadata)
        {
            byIndex.TryAdd(page.Image, page);
        }

        for (var i = 0; i < pages.Count; i++)
        {
            matched[i] = byIndex.GetValueOrDefault(i);
        }

        return matched;
    }

    /// <summary>
    /// Matches on the <c>Key</c> attribute, succeeding only when every metadata entry has a key and
    /// every key resolves to exactly one page.
    /// </summary>
    private static bool TryMatchByKey(
        IReadOnlyList<ComicArchiveEntry> pages,
        IReadOnlyList<PageMetadata> metadata,
        PageMetadata?[] matched)
    {
        if (metadata.Any(page => string.IsNullOrEmpty(page.Key)))
        {
            return false;
        }

        var byKey = new Dictionary<string, PageMetadata>(metadata.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var page in metadata)
        {
            if (!byKey.TryAdd(page.Key!, page))
            {
                return false;
            }
        }

        var candidate = new PageMetadata?[pages.Count];
        for (var i = 0; i < pages.Count; i++)
        {
            // A key can name the entry either by its full path or by its file name alone.
            if (byKey.TryGetValue(pages[i].Key, out var page)
                || byKey.TryGetValue(pages[i].FileName, out page))
            {
                candidate[i] = page;
            }
        }

        // Every declared page must resolve; otherwise the keys describe a different archive and
        // positional matching is used instead.
        if (candidate.Count(page => page is not null) != metadata.Count)
        {
            return false;
        }

        candidate.CopyTo(matched, 0);
        return true;
    }
}
