namespace ComiX;

/// <summary>
/// The editorial role of a page, as declared by the comic's metadata.
/// </summary>
public enum ComicPageType
{
    /// <summary>The metadata did not classify this page.</summary>
    Unknown = 0,

    /// <summary>The front cover.</summary>
    FrontCover = 1,

    /// <summary>An inner cover.</summary>
    InnerCover = 2,

    /// <summary>A recap or roundup page.</summary>
    Roundup = 3,

    /// <summary>A regular story page.</summary>
    Story = 4,

    /// <summary>An advertisement.</summary>
    Advertisement = 5,

    /// <summary>An editorial page.</summary>
    Editorial = 6,

    /// <summary>A letters page.</summary>
    Letters = 7,

    /// <summary>A preview of another comic.</summary>
    Preview = 8,

    /// <summary>The back cover.</summary>
    BackCover = 9,

    /// <summary>A page the metadata classifies as something else.</summary>
    Other = 10,

    /// <summary>A page the metadata marks as deleted.</summary>
    Deleted = 11,
}
