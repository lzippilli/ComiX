using ComiX.Archives;
using ComiX.Internal;

namespace ComiX;

/// <summary>
/// A non-image file carried inside a comic archive, such as <c>ComicInfo.xml</c> or a text file.
/// </summary>
/// <remarks>
/// Resources provide the same content access API as pages and are copied unchanged during export
/// unless <see cref="ComicExportOptions.IncludeResources"/> is disabled.
/// </remarks>
public sealed class ComicResource
{
    private readonly ComicContentReader _reader;
    private readonly ComicArchiveEntry _entry;

    internal ComicResource(ComicContentReader reader, ComicArchiveEntry entry)
    {
        _reader = reader;
        _entry = entry;
    }

    /// <summary>The archive entry path, using <c>/</c> as separator.</summary>
    public string Name => _entry.Key;

    /// <summary>The entry file name, without its directory path.</summary>
    public string FileName => _entry.FileName;

    /// <summary>The uncompressed size declared by the archive, in bytes. Declared sizes are not verified.</summary>
    public long SizeInBytes => _entry.Size;

    /// <summary>
    /// Reads the resource and returns a seekable, read-only stream over its content. The stream is
    /// owned by the caller and remains valid after the originating <see cref="ComicBook"/> is disposed.
    /// </summary>
    public Task<Stream> OpenAsync(CancellationToken cancellationToken = default) =>
        _reader.OpenAsync(_entry, cancellationToken);

    /// <summary>Writes the resource's content to <paramref name="destination"/>, which is not disposed.</summary>
    public Task ExtractAsync(Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        return _reader.CopyToAsync(_entry, destination, cancellationToken);
    }

    /// <summary>Writes the resource's content to a file, creating any missing directories.</summary>
    public Task ExtractAsync(
        string destinationPath,
        bool overwrite = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        return _reader.ExtractToFileAsync(_entry, destinationPath, overwrite, cancellationToken);
    }

    /// <inheritdoc />
    public override string ToString() => Name;
}
