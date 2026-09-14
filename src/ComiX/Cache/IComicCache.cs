namespace ComiX.Cache;

/// <summary>
/// Stores extracted entry content, so that repeated reads of the same entry are not decompressed
/// repeatedly. Internal by design: consumers select an implementation through <see cref="ComicCache"/>.
/// </summary>
internal interface IComicCache
{
    /// <summary>Returns cached content, or <see langword="null"/> when the entry is not cached.</summary>
    /// <param name="scope">Identifies the archive the entry belongs to.</param>
    /// <param name="key">Identifies the entry within the archive.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    ValueTask<byte[]?> TryGetAsync(string scope, string key, CancellationToken cancellationToken);

    /// <summary>Stores content for later reads. Implementations may decline to store it.</summary>
    ValueTask SetAsync(string scope, string key, byte[] content, CancellationToken cancellationToken);
}
