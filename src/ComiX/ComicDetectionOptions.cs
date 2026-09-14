namespace ComiX;

/// <summary>
/// How much of a file <see cref="ComicDetector"/> is allowed to read.
/// </summary>
public enum ComicDetectionDepth
{
    /// <summary>
    /// Reads only the file header to identify the container. Cost is independent of archive size;
    /// page count, metadata presence and encryption are not reported.
    /// </summary>
    Header = 0,

    /// <summary>
    /// Additionally reads the container index to report page count, metadata presence and encryption.
    /// Page content is not decompressed; cost scales with the number of entries.
    /// </summary>
    Contents = 1,
}

/// <summary>
/// Options for <see cref="ComicDetector"/>.
/// </summary>
public sealed record ComicDetectionOptions
{
    /// <summary>The options used when none are supplied.</summary>
    public static ComicDetectionOptions Default { get; } = new();

    /// <summary>
    /// How much of the file to read. Defaults to <see cref="ComicDetectionDepth.Contents"/>.
    /// </summary>
    public ComicDetectionDepth Depth { get; init; } = ComicDetectionDepth.Contents;
}
