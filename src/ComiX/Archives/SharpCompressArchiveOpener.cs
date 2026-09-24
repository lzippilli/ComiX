using SharpCompress.Archives;
using SharpCompress.Readers;
using RarFile = SharpCompress.Archives.Rar.RarArchive;
using SevenZipFile = SharpCompress.Archives.SevenZip.SevenZipArchive;
using TarFile = SharpCompress.Archives.Tar.TarArchive;
using ZipFile = SharpCompress.Archives.Zip.ZipArchive;

namespace ComiX.Archives;

/// <summary>
/// Opens a SharpCompress archive of one container format, either for the lifetime of a
/// <see cref="SharpCompressComicArchive"/> or for a single sequential pass.
/// </summary>
internal sealed class SharpCompressArchiveOpener
{
    private readonly ComicContainerFormat _format;
    private readonly ComicArchiveOptions _options;

    public SharpCompressArchiveOpener(ComicContainerFormat format, ComicArchiveOptions options)
    {
        _format = format;
        _options = options;
    }

    /// <summary>Opens <paramref name="stream"/> at its current position.</summary>
    /// <param name="stream">The archive content. Must be seekable. Not disposed by the archive returned.</param>
    /// <exception cref="UnsupportedComicFormatException">The format is not one ComiX can read.</exception>
    /// <remarks>
    /// SharpCompress failures are not converted here. Headers and entries are read lazily, so a
    /// malformed container can fail during this call or well after it, and the caller converts both.
    /// </remarks>
    public IArchive Open(Stream stream)
    {
        var options = CreateReaderOptions();

        return _format switch
        {
            ComicContainerFormat.Cbz => ZipFile.Open(stream, options),
            ComicContainerFormat.Cbr => RarFile.Open(stream, options),
            ComicContainerFormat.Cb7 => SevenZipFile.Open(stream, options),
            ComicContainerFormat.Cbt => TarFile.Open(stream, options),
            _ => throw new UnsupportedComicFormatException(
                "The file is not a comic archive in a recognised container format.",
                _format),
        };
    }

    /// <summary>
    /// Opens the same archive again, positioned at its start, for a single sequential pass.
    /// </summary>
    /// <param name="stream">The stream the archive was originally opened from.</param>
    /// <returns>A session owning only what this call created.</returns>
    /// <remarks>
    /// When <see cref="ComicArchiveOptions.SourcePath"/> is set the session gets a file of its own, so
    /// that the stream already in use is left untouched. Otherwise the caller's stream is rewound and
    /// left open; the archive holding it recovers because it seeks per entry.
    /// </remarks>
    public ArchiveSession Reopen(Stream stream)
    {
        if (_options.SourcePath is not { Length: > 0 } path)
        {
            stream.Position = 0;
            return new ArchiveSession(Open(stream), OwnedStream: null);
        }

        var file = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.SequentialScan);

        try
        {
            return new ArchiveSession(Open(file), file);
        }
        catch
        {
            file.Dispose();
            throw;
        }
    }

    private ReaderOptions CreateReaderOptions()
    {
        // A fresh instance per archive: SharpCompress treats these options as its own.
        var options = new ReaderOptions { LeaveStreamOpen = true, Password = _options.Password };

        if (_options.EntryNameEncoding is { } encoding)
        {
            // Default, not Forced: entries that declare UTF-8 must keep being decoded as UTF-8.
            options.ArchiveEncoding.Default = encoding;
        }

        return options;
    }
}
