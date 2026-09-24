using System.Text;

namespace ComiX.Archives;

/// <summary>
/// The settings an archive is opened and read with.
/// </summary>
/// <param name="Password">The password for an encrypted archive, or <see langword="null"/> when none is known.</param>
/// <param name="EntryNameEncoding">
/// The encoding for entry names the archive does not declare as UTF-8, or <see langword="null"/> for
/// the default.
/// </param>
/// <param name="SourcePath">
/// The file the archive was opened from, or <see langword="null"/> when it was opened from a stream.
/// A reopen uses it to avoid disturbing the stream already in use.
/// </param>
/// <param name="MaxEntrySizeInBytes">
/// The ceiling for content buffered inside the archive while a read is verified. The limit on what
/// reaches the caller is applied by <see cref="Internal.ComicContentReader"/>.
/// </param>
internal readonly record struct ComicArchiveOptions(
    string? Password,
    Encoding? EntryNameEncoding,
    string? SourcePath = null,
    long MaxEntrySizeInBytes = 256L * 1024 * 1024)
{
    /// <summary>Settings for an archive that needs no password and no encoding override.</summary>
    public static ComicArchiveOptions Default => new(Password: null, EntryNameEncoding: null);
}
