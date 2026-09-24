using ComiX.Internal;

namespace ComiX.Archives;

/// <summary>
/// Reads an entry by decompressing entries in their stored order until the requested one is reached.
/// </summary>
/// <remarks>
/// The position reached is kept between reads, so consecutive requests that move forward through the
/// archive share one traversal instead of restarting it. A request for an entry already passed
/// restarts the traversal, which is the only way a solid container can go backwards.
/// </remarks>
internal sealed class SequentialEntryAccess : IEntryAccess
{
    private readonly ArchiveEnumerator _entries;

    public SequentialEntryAccess(Func<ArchiveSession> reopen, int entryCount) =>
        _entries = new ArchiveEnumerator(reopen, entryCount);

    public async ValueTask CopyEntryToAsync(
        ComicArchiveEntry entry,
        Stream destination,
        CancellationToken cancellationToken)
    {
        if (_entries.HasPassed(entry.Key))
        {
            _entries.Reset();
        }

        var fromTheBeginning = _entries.AtStart;

        if (await TryCopyAsync(entry, destination, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        // The traversal ran out before reaching the entry. Having started part-way through the
        // archive is not evidence that the entry is absent, and a failed traversal leaves the
        // enumeration back at the beginning, so it is searched for once more.
        if (!fromTheBeginning
            && await TryCopyAsync(entry, destination, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        throw new ComicArchiveException($"Entry '{entry.Key}' is no longer present in the archive.", entry.Key);
    }

    public void Dispose() => _entries.Dispose();

    private async ValueTask<bool> TryCopyAsync(
        ComicArchiveEntry entry,
        Stream destination,
        CancellationToken cancellationToken)
    {
        while (_entries.MoveNext())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var candidate = _entries.Current;
            var isTarget = ArchiveIndex.Normalise(candidate.Key!).Equals(entry.Key, StringComparison.Ordinal);

            // OpenEntryStreamAsync must not be used: on a solid archive SharpCompress 1.0.0 returns
            // content decoded against the wrong window. See docs/architecture.md, section 18a.
            using (var source = candidate.OpenEntryStream())
            {
                // Discarded, but decompressed: an entry before the target contributes the state the
                // target decodes against.
                await source.CopyToAsync(
                        isTarget ? destination : Stream.Null,
                        StreamCopy.BufferSize,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            if (!isTarget)
            {
                continue;
            }

            // The target was the last entry, so the next request has to go backwards regardless. The
            // archive and its decompression window are released now rather than held until then.
            if (_entries.AtEnd)
            {
                _entries.Reset();
            }

            return true;
        }

        // Nothing further can be read from this traversal, and the next request must go backwards
        // anyway, so the archive and its decompression window are released now rather than held.
        _entries.Reset();
        return false;
    }
}
