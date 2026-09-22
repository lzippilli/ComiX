using System.Text;

namespace ComiX.Archives;

/// <summary>
/// Settings the archive implementation needs, carried as one value so that adding a setting does not
/// change every call site.
/// </summary>
/// <param name="Password">The password for encrypted archives, when one is known.</param>
/// <param name="EntryNameEncoding">
/// The encoding assumed for entry names the archive does not declare as UTF-8, or
/// <see langword="null"/> for the default.
/// </param>
internal readonly record struct ComicArchiveOptions(string? Password, Encoding? EntryNameEncoding)
{
    /// <summary>Settings for reading an archive that needs no password and no encoding override.</summary>
    public static ComicArchiveOptions Default => default;
}
