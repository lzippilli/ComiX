namespace ComiX;

/// <summary>
/// The severity of a validation finding.
/// </summary>
public enum ComicValidationSeverity
{
    /// <summary>An anomaly that does not prevent the archive from being used.</summary>
    Warning = 0,

    /// <summary>A defect that prevents the archive from being used as a comic.</summary>
    Error = 1,
}

/// <summary>
/// Identifies the subject of a validation finding, allowing callers to handle specific conditions
/// without matching on message text.
/// </summary>
public enum ComicValidationCode
{
    /// <summary>The container format could not be identified.</summary>
    ContainerNotRecognised = 1,

    /// <summary>The container format was identified but is not supported by this version.</summary>
    ContainerNotSupported = 2,

    /// <summary>The container is recognised but malformed.</summary>
    ContainerUnreadable = 3,

    /// <summary>The archive contains no images.</summary>
    NoPages = 4,

    /// <summary>An entry's content could not be read.</summary>
    EntryUnreadable = 5,

    /// <summary>An entry is encrypted and cannot be read without a password.</summary>
    EncryptedContent = 6,

    /// <summary>An entry is named so that extracting it would write outside the destination directory.</summary>
    PathTraversalEntry = 7,

    /// <summary>The file extension does not correspond to the detected container format.</summary>
    ExtensionMismatch = 8,

    /// <summary>A metadata source is present but could not be parsed.</summary>
    MetadataUnreadable = 9,

    /// <summary>
    /// The metadata contains an unrecognised field. Its value is retained in
    /// <see cref="ComicMetadata.Extensions"/>.
    /// </summary>
    UnknownMetadataField = 10,

    /// <summary>The page count declared in the metadata does not match the number of images found.</summary>
    MetadataPageCountMismatch = 11,

    /// <summary>The archive contains more than one entry with the same name; only the first is used.</summary>
    DuplicateEntryName = 12,

    /// <summary>An entry is neither a supported image nor a recognised metadata file.</summary>
    UnsupportedEntryFormat = 13,

    /// <summary>A page's content does not begin with a signature matching its file extension.</summary>
    ImageContentMismatch = 14,

    /// <summary>An entry contains no data.</summary>
    EmptyEntry = 15,

    /// <summary>The archive is one part of a multi-volume set; the remaining content is unavailable.</summary>
    IncompleteArchive = 16,

    /// <summary>
    /// The metadata describes a different number of pages than the archive contains; per-page
    /// information may be misattributed.
    /// </summary>
    PageMetadataMismatch = 17,
}

/// <summary>
/// A single finding produced by <see cref="ComicValidator"/>.
/// </summary>
/// <param name="Severity">The severity of the finding.</param>
/// <param name="Code">The condition the finding reports.</param>
/// <param name="Message">A description of the finding.</param>
/// <param name="EntryName">The archive entry concerned, for entry-specific findings.</param>
public sealed record ComicValidationIssue(
    ComicValidationSeverity Severity,
    ComicValidationCode Code,
    string Message,
    string? EntryName = null)
{
    /// <inheritdoc />
    public override string ToString() =>
        EntryName is null ? $"{Severity}: {Message}" : $"{Severity}: {Message} ({EntryName})";
}
