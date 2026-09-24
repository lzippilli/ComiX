namespace ComiX.Archives;

/// <summary>
/// One entry inside a comic archive, normalised across container formats.
/// </summary>
/// <param name="Key">The entry path, always using <c>/</c> as separator.</param>
/// <param name="Size">The uncompressed size declared by the container. Declared sizes are not verified.</param>
/// <param name="IsDirectory">Whether the entry is a directory rather than content.</param>
/// <param name="IsEncrypted">Whether the entry's content is encrypted.</param>
/// <param name="LastModified">The entry's last modification time, when the container records one.</param>
internal sealed record ComicArchiveEntry(
    string Key,
    long Size,
    bool IsDirectory,
    bool IsEncrypted,
    DateTime? LastModified)
{
    /// <summary>
    /// The CRC-32 the container records for the entry's uncompressed content, or <c>0</c> when it
    /// records none. The two cannot be distinguished: TAR reports <c>0</c> for every entry, and a
    /// container that omits the field reports the same value as one that recorded a checksum of zero.
    /// Consumers treat <c>0</c> as no checksum.
    /// </summary>
    public long Crc { get; init; }

    /// <summary>The entry's file name without its directory path.</summary>
    public string FileName { get; } = Internal.PathSafety.GetFileName(Key);

    /// <summary>The entry's directory path, or an empty string for entries at the archive root.</summary>
    public string DirectoryName { get; } = Internal.PathSafety.GetDirectoryName(Key);
}
