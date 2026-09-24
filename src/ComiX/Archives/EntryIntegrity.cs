using ComiX.Internal;

namespace ComiX.Archives;

/// <summary>
/// Checks content read from an archive against what the container declares for the entry.
/// </summary>
/// <remarks>
/// This compares the container's own headers with what that same container produced. It detects a
/// read decoded against the wrong compression state; it says nothing about whether the archive
/// describes the original file correctly.
/// </remarks>
internal static class EntryIntegrity
{
    /// <summary>
    /// Determines whether <paramref name="content"/> matches the checksum declared for
    /// <paramref name="entry"/>, or its declared length where no checksum is available.
    /// </summary>
    public static bool Matches(ComicArchiveEntry entry, ReadOnlySpan<byte> content) =>
        entry.Crc != 0
            ? Crc32.Compute(content) == (uint)entry.Crc
            : entry.Size < 0 || content.Length == entry.Size;
}
