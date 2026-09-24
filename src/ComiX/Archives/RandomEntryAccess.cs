using ComiX.Internal;
using SharpCompress.Archives;

namespace ComiX.Archives;

/// <summary>
/// Reads an entry by opening it directly.
/// </summary>
internal sealed class RandomEntryAccess : IEntryAccess
{
    private readonly IReadOnlyDictionary<string, IArchiveEntry> _entriesByKey;

    public RandomEntryAccess(IReadOnlyDictionary<string, IArchiveEntry> entriesByKey) =>
        _entriesByKey = entriesByKey;

    public async ValueTask CopyEntryToAsync(
        ComicArchiveEntry entry,
        Stream destination,
        CancellationToken cancellationToken)
    {
        if (!_entriesByKey.TryGetValue(entry.Key, out var archiveEntry))
        {
            throw new ComicArchiveException($"Entry '{entry.Key}' is no longer present in the archive.", entry.Key);
        }

        // OpenEntryStreamAsync must not be used: on a solid archive SharpCompress 1.0.0 returns
        // content decoded against the wrong window. See docs/architecture.md, section 18a.
        using var source = archiveEntry.OpenEntryStream();
        await source.CopyToAsync(destination, StreamCopy.BufferSize, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Holds nothing of its own: the archive it reads from belongs to the caller.</summary>
    public void Dispose()
    {
    }
}
