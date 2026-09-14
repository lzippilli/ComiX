namespace ComiX.Archives;

/// <summary>
/// Maps a detected container format to its archive implementation. Sole authority on which formats
/// are supported, shared by opening, detection and validation.
/// </summary>
internal static class ComicArchiveFactory
{
    /// <summary>Whether ComiX can read the given container format.</summary>
    public static bool IsSupported(ComicContainerFormat format) =>
        format is ComicContainerFormat.Cbz
            or ComicContainerFormat.Cbr
            or ComicContainerFormat.Cb7
            or ComicContainerFormat.Cbt;

    /// <summary>Opens <paramref name="stream"/> as the given format.</summary>
    /// <exception cref="UnsupportedComicFormatException">The format is not one ComiX can read.</exception>
    /// <exception cref="ComicArchiveException">The container is malformed.</exception>
    public static IComicArchive Open(
        Stream stream,
        ComicContainerFormat format,
        bool ownsStream,
        string? password)
    {
        if (!IsSupported(format))
        {
            throw new UnsupportedComicFormatException(
                "The file is not a comic archive in a recognised container format.",
                format);
        }

        return SharpCompressComicArchive.Open(stream, format, ownsStream, password);
    }
}
