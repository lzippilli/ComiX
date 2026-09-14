namespace ComiX;

/// <summary>
/// Options for exporting a comic to another container format.
/// </summary>
public sealed record ComicExportOptions
{
    /// <summary>The options used when none are supplied.</summary>
    public static ComicExportOptions Default { get; } = new();

    /// <summary>Whether to replace an existing destination file instead of failing.</summary>
    public bool Overwrite { get; init; }

    /// <summary>
    /// Copies non-image entries, including the metadata file, into the exported archive. Enabled by
    /// default; disabling it produces an archive containing pages only.
    /// </summary>
    public bool IncludeResources { get; init; } = true;
}
