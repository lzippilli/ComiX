using SharpCompress.Archives;

namespace ComiX.Archives;

/// <summary>
/// Selects the entry access strategy for an open archive.
/// </summary>
internal static class EntryAccess
{
    /// <summary>Builds the strategy for <paramref name="archive"/>.</summary>
    /// <param name="archive">The open archive the strategy reads from.</param>
    /// <param name="index">The archive's indexed entries.</param>
    /// <param name="reopen">Opens the archive again for a single sequential pass.</param>
    /// <param name="maxEntrySize">The ceiling for content buffered while a read is verified.</param>
    /// <remarks>
    /// A solid archive cannot always produce an entry through direct access, so direct access becomes
    /// the primary of a verified fallback rather than the whole strategy. The selection follows the
    /// archive's solidity rather than its format: the fallback only runs after a direct read has
    /// failed to verify, so applying it to every solid container costs nothing.
    /// </remarks>
    public static IEntryAccess For(
        IArchive archive,
        ArchiveIndex index,
        Func<ArchiveSession> reopen,
        long maxEntrySize)
    {
        var direct = new RandomEntryAccess(index.ByKey);

        return archive.IsSolid
            ? new ResilientEntryAccess(
                direct,
                new SequentialEntryAccess(reopen, index.ContentEntryCount),
                maxEntrySize)
            : direct;
    }
}
