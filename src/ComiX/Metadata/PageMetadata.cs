namespace ComiX.Metadata;

/// <summary>
/// Per-page information declared by a metadata source.
/// </summary>
/// <param name="Image">The zero-based page index the source refers to.</param>
/// <param name="Key">
/// The entry name the source associates with this page. ComicInfo's <c>Key</c> attribute is optional
/// and free-form; applications that populate it record the file name, which matches more reliably
/// than a positional index.
/// </param>
/// <param name="Type">The page's declared editorial role.</param>
/// <param name="Spread">Whether the page is declared as a spread.</param>
/// <param name="Width">The declared pixel width, if any. Not measured from the image.</param>
/// <param name="Height">The declared pixel height, if any.</param>
/// <param name="Bookmark">A bookmark label for the page.</param>
internal sealed record PageMetadata(
    int Image,
    string? Key,
    ComicPageType Type,
    ComicPageSpread Spread,
    int? Width,
    int? Height,
    string? Bookmark);
