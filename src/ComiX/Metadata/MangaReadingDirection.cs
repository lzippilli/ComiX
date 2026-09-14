namespace ComiX;

/// <summary>
/// Whether a comic is manga, and if so which direction it reads.
/// </summary>
public enum MangaReadingDirection
{
    /// <summary>The source metadata did not say.</summary>
    Unknown = 0,

    /// <summary>Explicitly not manga.</summary>
    No = 1,

    /// <summary>Manga, read left to right.</summary>
    Yes = 2,

    /// <summary>Manga, read right to left.</summary>
    YesRightToLeft = 3,
}
