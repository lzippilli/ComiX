using ComiX.Internal;

namespace ComiX.Archives;

/// <summary>
/// Reads an entry through two strategies, using whichever produces content matching what the
/// container declares for the entry.
/// </summary>
/// <remarks>
/// Direct access is tried first until it fails for an entry, after which the sequential strategy
/// leads until it fails in turn. Reads are serialised by the archive, so the preference needs no
/// synchronisation.
/// </remarks>
internal sealed class ResilientEntryAccess : IEntryAccess
{
    private const int InitialBufferCap = 1024 * 1024;

    private readonly IEntryAccess _primary;
    private readonly IEntryAccess _fallback;
    private readonly long _maxEntrySize;

    private bool _preferFallback;

    public ResilientEntryAccess(IEntryAccess primary, IEntryAccess fallback, long maxEntrySize)
    {
        _primary = primary;
        _fallback = fallback;
        _maxEntrySize = maxEntrySize;
    }

    public async ValueTask CopyEntryToAsync(
        ComicArchiveEntry entry,
        Stream destination,
        CancellationToken cancellationToken)
    {
        // Content reaches the destination only once it has been verified, so a fallback never appends
        // to bytes the caller already holds.
        using var content = await ReadAsync(entry, cancellationToken).ConfigureAwait(false);

        content.Position = 0;
        await content.CopyToAsync(destination, StreamCopy.BufferSize, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<MemoryStream> ReadAsync(ComicArchiveEntry entry, CancellationToken cancellationToken)
    {
        // The strategy that served the previous read is tried first. On an archive where direct access
        // never works, this saves a failed attempt per entry; on one where it always works, the
        // preference never moves. One read of the other strategy is enough to change it back.
        var preferred = _preferFallback ? _fallback : _primary;
        var other = _preferFallback ? _primary : _fallback;

        var content = await TryAsync(preferred, entry, cancellationToken).ConfigureAwait(false);
        if (content is not null)
        {
            return content;
        }

        content = await TryAsync(other, entry, cancellationToken).ConfigureAwait(false);
        if (content is not null)
        {
            _preferFallback = !_preferFallback;
            return content;
        }

        throw new ComicArchiveException(
            $"Entry '{entry.Key}' did not match the checksum recorded for it. The archive is damaged.",
            entry.Key);
    }

    /// <summary>
    /// Reads the entry through one strategy and verifies what it produced.
    /// </summary>
    /// <returns>The content read, or <see langword="null"/> when the other strategy should be tried.</returns>
    private async ValueTask<MemoryStream?> TryAsync(
        IEntryAccess access,
        ComicArchiveEntry entry,
        CancellationToken cancellationToken)
    {
        var buffer = NewBuffer(entry);
        try
        {
            await access.CopyEntryToAsync(entry, Bounded(buffer, entry), cancellationToken).ConfigureAwait(false);

            if (Matches(entry, buffer))
            {
                return buffer;
            }
        }
        catch (Exception ex) when (ex is not ComiXException and not OperationCanceledException)
        {
            // Deliberately narrow. A cancelled read must not start a second attempt, and a
            // ComiXException reports a condition a second attempt cannot change: a missing entry, or
            // content over the configured size limit.
        }

        buffer.Dispose();
        return null;
    }

    public void Dispose()
    {
        _primary.Dispose();
        _fallback.Dispose();
    }

    private static bool Matches(ComicArchiveEntry entry, MemoryStream buffer) =>
        buffer.TryGetBuffer(out var segment)
            ? EntryIntegrity.Matches(entry, segment.AsSpan())
            : EntryIntegrity.Matches(entry, buffer.ToArray());

    /// <summary>The declared size sizes the buffer; it does not bound it.</summary>
    private static MemoryStream NewBuffer(ComicArchiveEntry entry) =>
        new((int)Math.Clamp(entry.Size, 0, InitialBufferCap));

    private Stream Bounded(MemoryStream buffer, ComicArchiveEntry entry) =>
        new LimitedStream(buffer, _maxEntrySize, entry.Key);
}
