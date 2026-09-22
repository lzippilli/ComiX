using ComiX.Archives;
using ComiX.Cache;
using ComiX.Detection;
using ComiX.Internal;
using ComiX.Metadata;

namespace ComiX;

/// <summary>
/// Reports defects in a comic archive as diagnostics rather than exceptions.
/// </summary>
/// <remarks>
/// Validation reads the container index. <see cref="ComicValidationOptions.DeepScan"/> additionally
/// decompresses every page, which reads the entire archive.
/// </remarks>
public static class ComicValidator
{
    /// <summary>Validates the comic archive at <paramref name="path"/>.</summary>
    /// <param name="path">The file to validate.</param>
    /// <param name="options">Validation options, or <see langword="null"/> for <see cref="ComicValidationOptions.Default"/>.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    public static async Task<ComicValidationResult> ValidateAsync(
        string path,
        ComicValidationOptions? options = null,
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
            return await ValidateCoreAsync(
                    stream,
                    extension.Length > 0 ? extension : null,
                    options ?? ComicValidationOptions.Default,
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>Validates the comic archive in <paramref name="stream"/>, which is not disposed.</summary>
    /// <param name="stream">The content to validate. Must be readable and seekable.</param>
    /// <param name="options">Validation options, or <see langword="null"/> for <see cref="ComicValidationOptions.Default"/>.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <exception cref="ArgumentException">The stream is not readable or not seekable.</exception>
    public static Task<ComicValidationResult> ValidateAsync(
        Stream stream,
        ComicValidationOptions? options = null,
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

        return ValidateCoreAsync(stream, null, options ?? ComicValidationOptions.Default, cancellationToken);
    }

    private static async Task<ComicValidationResult> ValidateCoreAsync(
        Stream stream,
        string? fileExtension,
        ComicValidationOptions options,
        CancellationToken cancellationToken)
    {
        var issues = new List<ComicValidationIssue>();

        var header = await HeaderReader.ReadAsync(stream, cancellationToken).ConfigureAwait(false);
        var format = ContainerSignature.Identify(header);

        if (format is ComicContainerFormat.Unknown)
        {
            issues.Add(new ComicValidationIssue(
                ComicValidationSeverity.Error,
                ComicValidationCode.ContainerNotRecognised,
                "The file is not a recognised comic archive container."));

            return new ComicValidationResult { Issues = issues, Format = format };
        }

        if (fileExtension is not null
            && !string.Equals(
                fileExtension,
                ContainerSignature.ConventionalExtension(format),
                StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ComicValidationIssue(
                ComicValidationSeverity.Warning,
                ComicValidationCode.ExtensionMismatch,
                $"The file is named '{fileExtension}' but its content is {format}."));
        }

        if (!ComicArchiveFactory.IsSupported(format))
        {
            issues.Add(new ComicValidationIssue(
                ComicValidationSeverity.Error,
                ComicValidationCode.ContainerNotSupported,
                $"{format} archives are recognised but not supported by this version of ComiX."));

            return new ComicValidationResult { Issues = issues, Format = format };
        }

        IComicArchive archive;
        try
        {
            archive = ComicArchiveFactory.Open(
                stream,
                format,
                ownsStream: false,
                new ComicArchiveOptions(options.Password, options.EntryNameEncoding));
        }
        catch (ComicArchiveException ex)
        {
            issues.Add(new ComicValidationIssue(
                ComicValidationSeverity.Error,
                ComicValidationCode.ContainerUnreadable,
                ex.Message));

            return new ComicValidationResult { Issues = issues, Format = format };
        }

        using (archive)
        {
            var pages = ArchiveContent.SelectPages(archive.Entries);
            ValidateEntries(archive, pages, issues);
            await ValidateMetadataAsync(archive, pages.Count, options, issues, cancellationToken)
                .ConfigureAwait(false);

            if (options.DeepScan)
            {
                await DeepScanAsync(archive, pages, options, issues, cancellationToken).ConfigureAwait(false);
            }

            return new ComicValidationResult
            {
                Issues = issues,
                Format = format,
                PageCount = pages.Count,
            };
        }
    }

    /// <summary>
    /// Whether a decoded entry name shows the signature of an encoding mismatch: a Unicode
    /// replacement character, or a control character, neither of which a writer produces
    /// intentionally.
    /// </summary>
    private static bool HasSuspectEncoding(string entryName)
    {
        foreach (var character in entryName)
        {
            if (character == '�' || char.IsControl(character))
            {
                return true;
            }
        }

        return false;
    }

    private static void ValidateEntries(
        IComicArchive archive,
        List<ComicArchiveEntry> pages,
        List<ComicValidationIssue> issues)
    {
        if (!archive.IsComplete)
        {
            issues.Add(new ComicValidationIssue(
                ComicValidationSeverity.Error,
                ComicValidationCode.IncompleteArchive,
                "The archive is one part of a multi-volume set, so some content is missing."));
        }

        foreach (var duplicate in archive.DuplicateEntryNames)
        {
            issues.Add(new ComicValidationIssue(
                ComicValidationSeverity.Warning,
                ComicValidationCode.DuplicateEntryName,
                "The archive contains more than one entry with this name; only the first is used.",
                duplicate));
        }

        foreach (var entry in archive.Entries)
        {
            if (HasSuspectEncoding(entry.Key))
            {
                issues.Add(new ComicValidationIssue(
                    ComicValidationSeverity.Warning,
                    ComicValidationCode.EntryNameEncodingSuspect,
                    "The entry name contains replacement or control characters, indicating that it was "
                    + "written in an encoding the archive does not declare. Set "
                    + $"{nameof(ComicOpenOptions.EntryNameEncoding)} to read it correctly.",
                    entry.Key));
            }

            if (PathSafety.IsTraversal(entry.Key))
            {
                issues.Add(new ComicValidationIssue(
                    ComicValidationSeverity.Error,
                    ComicValidationCode.PathTraversalEntry,
                    "The entry is named so that extracting it would write outside the destination directory.",
                    entry.Key));
            }

            if (entry.IsEncrypted)
            {
                issues.Add(new ComicValidationIssue(
                    ComicValidationSeverity.Error,
                    ComicValidationCode.EncryptedContent,
                    "The entry is encrypted and cannot be read without a password.",
                    entry.Key));
            }

            if (!ArchiveContent.IsContent(entry))
            {
                continue;
            }

            if (ImageFormats.FromFileName(entry.FileName) is ComicImageFormat.Unknown
                && !ArchiveContent.IsMetadataFile(entry))
            {
                issues.Add(new ComicValidationIssue(
                    ComicValidationSeverity.Warning,
                    ComicValidationCode.UnsupportedEntryFormat,
                    "The entry is neither a supported image nor a recognised metadata file.",
                    entry.Key));
            }
        }

        if (pages.Count == 0)
        {
            issues.Add(new ComicValidationIssue(
                ComicValidationSeverity.Error,
                ComicValidationCode.NoPages,
                "The archive contains no images."));
        }
    }

    private static async Task ValidateMetadataAsync(
        IComicArchive archive,
        int pageCount,
        ComicValidationOptions options,
        List<ComicValidationIssue> issues,
        CancellationToken cancellationToken)
    {
        var reader = new ComicContentReader(
            archive,
            NullComicCache.Instance,
            cacheScope: string.Empty,
            options.MaxEntrySizeInBytes);

        var resolved = await MetadataResolver.ResolveAsync(archive, reader, cancellationToken)
            .ConfigureAwait(false);

        foreach (var failure in resolved.Failures)
        {
            issues.Add(new ComicValidationIssue(
                ComicValidationSeverity.Warning,
                ComicValidationCode.MetadataUnreadable,
                $"The {failure.Standard} metadata could not be read: {failure.Message}",
                failure.Location));
        }

        var document = resolved.Document;

        foreach (var field in document.UnknownFields.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            issues.Add(new ComicValidationIssue(
                ComicValidationSeverity.Warning,
                ComicValidationCode.UnknownMetadataField,
                $"The metadata contains an unrecognised field '{field}'. Its value is preserved in Metadata.Extensions."));
        }

        if (document.Metadata.DeclaredPageCount is { } declared && declared != pageCount)
        {
            issues.Add(new ComicValidationIssue(
                ComicValidationSeverity.Warning,
                ComicValidationCode.MetadataPageCountMismatch,
                $"The metadata declares {declared} pages but the archive contains {pageCount}."));
        }

        // Per-page information is matched positionally unless the source names its pages, so a
        // mismatched count indicates that page attributes may be misattributed.
        if (document.Pages.Count > 0 && document.Pages.Count != pageCount)
        {
            issues.Add(new ComicValidationIssue(
                ComicValidationSeverity.Warning,
                ComicValidationCode.PageMetadataMismatch,
                $"The metadata describes {document.Pages.Count} pages but the archive contains {pageCount}; "
                + "per-page information may be attached to the wrong pages."));
        }
    }

    private static async Task DeepScanAsync(
        IComicArchive archive,
        List<ComicArchiveEntry> pages,
        ComicValidationOptions options,
        List<ComicValidationIssue> issues,
        CancellationToken cancellationToken)
    {
        var reader = new ComicContentReader(
            archive,
            NullComicCache.Instance,
            cacheScope: string.Empty,
            options.MaxEntrySizeInBytes);

        foreach (var page in pages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sink = new SniffingSink();
            try
            {
                await reader.CopyToAsync(page, sink, cancellationToken).ConfigureAwait(false);
            }
            catch (ComicArchiveException ex)
            {
                issues.Add(new ComicValidationIssue(
                    ComicValidationSeverity.Error,
                    ComicValidationCode.EntryUnreadable,
                    ex.Message,
                    page.Key));
                continue;
            }

            if (sink.BytesWritten == 0)
            {
                issues.Add(new ComicValidationIssue(
                    ComicValidationSeverity.Warning,
                    ComicValidationCode.EmptyEntry,
                    "The page contains no data.",
                    page.Key));
                continue;
            }

            var declared = ImageFormats.FromFileName(page.FileName);
            var actual = ImageFormats.FromHeader(sink.Header);
            if (actual != declared)
            {
                issues.Add(new ComicValidationIssue(
                    ComicValidationSeverity.Warning,
                    ComicValidationCode.ImageContentMismatch,
                    actual is ComicImageFormat.Unknown
                        ? $"The page claims to be {declared} but its content does not start like any image format ComiX knows."
                        : $"The page claims to be {declared} but its content looks like {actual}.",
                    page.Key));
            }
        }
    }
}
