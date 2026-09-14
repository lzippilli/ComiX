namespace ComiX;

/// <summary>
/// The outcome of identifying a file with <see cref="ComicDetector"/>. Detection reports what a file
/// is; use <see cref="ComicValidator"/> to determine whether it is well formed.
/// </summary>
public sealed record ComicDetectionResult
{
    /// <summary>The container format identified from the file contents.</summary>
    public required ComicContainerFormat Format { get; init; }

    /// <summary>Whether the detected format can be opened by this version of the library.</summary>
    public required bool IsSupported { get; init; }

    /// <summary>
    /// The file extension including the leading dot, or <see langword="null"/> for streams and files
    /// without an extension.
    /// </summary>
    public string? FileExtension { get; init; }

    /// <summary>Whether the file extension corresponds to the detected format.</summary>
    public bool ExtensionMatchesFormat { get; init; }

    /// <summary>
    /// The number of pages, or <see langword="null"/> when the container index was not read: the
    /// format is unsupported, the container is unreadable, or detection ran at
    /// <see cref="ComicDetectionDepth.Header"/>.
    /// </summary>
    public int? PageCount { get; init; }

    /// <summary>
    /// Whether a supported metadata source was located. <see langword="false"/> when the container
    /// index was not read.
    /// </summary>
    public bool HasMetadata { get; init; }

    /// <summary>
    /// The metadata standard located. Detection locates the source but does not parse it, so the
    /// metadata is not guaranteed to be readable.
    /// </summary>
    public ComicMetadataStandard MetadataStandard { get; init; }

    /// <summary>
    /// Whether any entry is encrypted. <see langword="false"/> when the container index was not read.
    /// </summary>
    public bool HasEncryptedEntries { get; init; }

    /// <summary>The file size in bytes, or <see langword="null"/> when detecting from a stream.</summary>
    public long? SizeInBytes { get; init; }
}
