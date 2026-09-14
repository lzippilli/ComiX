namespace ComiX;

/// <summary>
/// An external identifier for a comic, such as a database id or a product code.
/// </summary>
/// <param name="Source">
/// Where the identifier comes from, lower-cased when recognised (for example <c>comicvine</c>,
/// <c>metron</c>, <c>gtin</c>, <c>isbn</c>).
/// </param>
/// <param name="Value">The identifier itself, preserved verbatim from the source metadata.</param>
public sealed record ComicIdentifier(string Source, string Value);
