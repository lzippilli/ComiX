namespace ComiX;

/// <summary>
/// Options for extracting a comic's contents to a directory.
/// </summary>
public sealed record ComicExtractionOptions
{
    /// <summary>The options used when none are supplied.</summary>
    public static ComicExtractionOptions Default { get; } = new();

    /// <summary>Whether to replace existing files instead of failing.</summary>
    public bool Overwrite { get; init; }

    /// <summary>Whether to extract non-image files as well as pages. Defaults to <see langword="true"/>.</summary>
    public bool IncludeResources { get; init; } = true;

    /// <summary>
    /// Writes all files directly into the destination directory instead of recreating the archive's
    /// directory structure. Colliding names then require <see cref="Overwrite"/>; otherwise
    /// extraction fails on the collision.
    /// </summary>
    public bool Flatten { get; init; }
}
