namespace ComiX.Metadata;

/// <summary>
/// The result of reading a single metadata source.
/// </summary>
/// <param name="Metadata">The normalised metadata.</param>
/// <param name="Pages">Per-page information, in source order. Provided only by ComicInfo.</param>
/// <param name="UnknownFields">
/// Unmapped field names, reported for validation. Their values are retained in
/// <see cref="ComicMetadata.Extensions"/>.
/// </param>
internal sealed record ComicMetadataDocument(
    ComicMetadata Metadata,
    IReadOnlyList<PageMetadata> Pages,
    IReadOnlyList<string> UnknownFields);

/// <summary>
/// A metadata source that was located but could not be parsed.
/// </summary>
/// <param name="Standard">The standard the source was expected to follow.</param>
/// <param name="Location">The source location.</param>
/// <param name="Message">The reason parsing failed.</param>
internal sealed record ComicMetadataFailure(
    ComicMetadataStandard Standard,
    string Location,
    string Message);

/// <summary>
/// The outcome of metadata resolution for one archive: the resolved document and any parsing
/// failures encountered.
/// </summary>
internal sealed record ResolvedMetadata(
    ComicMetadataDocument Document,
    IReadOnlyList<ComicMetadataFailure> Failures)
{
    public static ResolvedMetadata None { get; } =
        new(new ComicMetadataDocument(ComicMetadata.Empty, [], []), []);
}
