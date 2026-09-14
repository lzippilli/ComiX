namespace ComiX;

/// <summary>
/// The page layout declared by the source metadata.
/// </summary>
/// <remarks>
/// ComicInfo is the only supported standard that describes pages individually; CoMet, ComicBookInfo
/// and MetronInfo define a page count only. <see cref="Unknown"/> therefore applies to every page of
/// an archive tagged with those standards, to untagged archives, and to ComicInfo documents that omit
/// the attribute. ComiX does not decode image content and does not infer the layout.
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Naming",
    "CA1720:Identifier contains type name",
    Justification = "Single and Double denote page layout, not the numeric types. Renaming them would "
        + "obscure the domain concept the enumeration models.")]
public enum ComicPageSpread
{
    /// <summary>The source metadata does not declare a layout.</summary>
    Unknown = 0,

    /// <summary>Declared as a single page.</summary>
    Single = 1,

    /// <summary>Declared as a double-page spread.</summary>
    Double = 2,
}
