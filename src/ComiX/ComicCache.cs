using ComiX.Cache;

namespace ComiX;

/// <summary>
/// Selects the caching strategy for extracted entry content, supplied through
/// <see cref="ComicOpenOptions.Cache"/>.
/// </summary>
/// <remarks>
/// Caching is optional; the default is <see cref="None"/>. An instance may be shared by several
/// <see cref="ComicBook"/> instances, which then share a single budget. Cached content is keyed by
/// archive path, size and last write time, so a modified file does not return cached content.
/// </remarks>
public sealed class ComicCache
{
    private ComicCache(IComicCache implementation) => Implementation = implementation;

    internal IComicCache Implementation { get; }

    /// <summary>A cache that stores nothing.</summary>
    public static ComicCache None { get; } = new(NullComicCache.Instance);

    /// <summary>
    /// Creates an in-memory cache of extracted content, evicting least recently used entries once
    /// <paramref name="maxSizeInBytes"/> is reached.
    /// </summary>
    /// <param name="maxSizeInBytes">The total budget for cached content, 64 MiB by default.</param>
    /// <returns>A cache instance for use with <see cref="ComicOpenOptions.Cache"/>.</returns>
    public static ComicCache InMemory(long maxSizeInBytes = 64L * 1024 * 1024) =>
        new(new MemoryComicCache(maxSizeInBytes));
}
