using ComiX.Archives;
using ComiX.Detection;
using ComiX.Internal;
using ComiX.Metadata;

namespace ComiX;

/// <summary>
/// Identifies comic archives without opening them as a <see cref="ComicBook"/>.
/// </summary>
/// <remarks>
/// Detection reads the file header to identify the container and, unless
/// <see cref="ComicDetectionDepth.Header"/> is requested, the container index to report page count
/// and metadata presence. Page content is not decompressed.
/// </remarks>
public static class ComicDetector
{
    /// <summary>Identifies the comic archive at <paramref name="path"/>.</summary>
    /// <param name="path">The file to inspect.</param>
    /// <param name="options">Detection options, or <see langword="null"/> for <see cref="ComicDetectionOptions.Default"/>.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    public static async Task<ComicDetectionResult> DetectAsync(
        string path,
        ComicDetectionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var file = new FileInfo(path);
        if (!file.Exists)
        {
            throw new FileNotFoundException("The comic archive was not found.", path);
        }

        var stream = new FileStream(
            file.FullName,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.RandomAccess);

        await using (stream.ConfigureAwait(false))
        {
            var extension = Path.GetExtension(file.Name);
            return await DetectCoreAsync(
                    stream,
                    extension.Length > 0 ? extension : null,
                    file.Length,
                    options ?? ComicDetectionOptions.Default,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>Identifies the comic archive in <paramref name="stream"/>, which is not disposed.</summary>
    /// <param name="stream">The content to inspect. Must be readable and seekable.</param>
    /// <param name="options">Detection options, or <see langword="null"/> for <see cref="ComicDetectionOptions.Default"/>.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <exception cref="ArgumentException">The stream is not readable or not seekable.</exception>
    public static Task<ComicDetectionResult> DetectAsync(
        Stream stream,
        ComicDetectionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (!stream.CanRead)
        {
            throw new ArgumentException("The stream must be readable.", nameof(stream));
        }

        if (!stream.CanSeek)
        {
            throw new ArgumentException("The stream must be seekable.", nameof(stream));
        }

        return DetectCoreAsync(
            stream,
            fileExtension: null,
            sizeInBytes: null,
            options ?? ComicDetectionOptions.Default,
            cancellationToken);
    }

    private static async Task<ComicDetectionResult> DetectCoreAsync(
        Stream stream,
        string? fileExtension,
        long? sizeInBytes,
        ComicDetectionOptions options,
        CancellationToken cancellationToken)
    {
        var header = await HeaderReader.ReadAsync(stream, cancellationToken).ConfigureAwait(false);
        var format = ContainerSignature.Identify(header);

        var result = new ComicDetectionResult
        {
            Format = format,
            IsSupported = ComicArchiveFactory.IsSupported(format),
            FileExtension = fileExtension,
            SizeInBytes = sizeInBytes,
            ExtensionMatchesFormat = fileExtension is not null
                && string.Equals(
                    fileExtension,
                    ContainerSignature.ConventionalExtension(format),
                    StringComparison.OrdinalIgnoreCase),
        };

        if (!result.IsSupported || options.Depth is ComicDetectionDepth.Header)
        {
            return result;
        }

        try
        {
            using var archive = ComicArchiveFactory.Open(
                stream,
                format,
                ownsStream: false,
                ComicArchiveOptions.Default);
            cancellationToken.ThrowIfCancellationRequested();

            // The metadata source is located but not parsed; validation reports whether it is readable.
            var candidate = MetadataResolver.FindCandidates(archive).FirstOrDefault();
            return result with
            {
                PageCount = ArchiveContent.SelectPages(archive.Entries).Count,
                HasMetadata = candidate is not null,
                MetadataStandard = candidate?.Standard ?? ComicMetadataStandard.None,
                HasEncryptedEntries = archive.Entries.Any(entry => entry.IsEncrypted),
            };
        }
        catch (ComicArchiveException)
        {
            // The header identified the container but it could not be opened: report the format, and
            // let validation explain why it is unusable.
            return result;
        }
    }
}
