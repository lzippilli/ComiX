namespace ComiX;

/// <summary>
/// Comic metadata normalised into a single immutable model, independent of the standard that
/// supplied it.
/// </summary>
/// <remarks>
/// Properties are canonical only where the concept is common to several standards. Values specific to
/// one standard, and values ComiX does not recognise, are retained in <see cref="Extensions"/>.
/// Values are preserved as written in the source unless normalisation is unambiguous.
/// </remarks>
public sealed record ComicMetadata
{
    /// <summary>Metadata with no values and no sources.</summary>
    public static ComicMetadata Empty { get; } = new();

    /// <summary>
    /// Every metadata source located in the archive, including those that did not supply the
    /// canonical values.
    /// </summary>
    public IReadOnlyList<ComicMetadataSource> Sources { get; init; } = [];

    /// <summary>
    /// The source that supplied the canonical values, or <see langword="null"/> when no source could
    /// be parsed.
    /// </summary>
    public ComicMetadataSource? PrimarySource => Sources.FirstOrDefault(source => source.IsPrimary);

    /// <summary>The standard the canonical values came from, or <see cref="ComicMetadataStandard.None"/>.</summary>
    public ComicMetadataStandard Standard => PrimarySource?.Standard ?? ComicMetadataStandard.None;

    /// <summary>The title of this individual issue or volume.</summary>
    public string? Title { get; init; }

    /// <summary>The name of the series this comic belongs to.</summary>
    public string? Series { get; init; }

    /// <summary>A sortable form of <see cref="Series"/>, when the source provides one.</summary>
    public string? SeriesSort { get; init; }

    /// <summary>
    /// The issue number within the series. Represented as text because published values include
    /// non-numeric forms such as <c>1.5</c> or <c>Annual 1</c>.
    /// </summary>
    public string? Number { get; init; }

    /// <summary>The total number of issues in the series, when known.</summary>
    public int? Count { get; init; }

    /// <summary>
    /// The volume the issue belongs to. Represented as text because sources use both sequence numbers
    /// and years.
    /// </summary>
    public string? Volume { get; init; }

    /// <summary>An alternate series name, used for crossovers and reprints.</summary>
    public string? AlternateSeries { get; init; }

    /// <summary>The issue number within <see cref="AlternateSeries"/>.</summary>
    public string? AlternateNumber { get; init; }

    /// <summary>A synopsis of the issue.</summary>
    public string? Summary { get; init; }

    /// <summary>Free-form notes, commonly written by the tagging application.</summary>
    public string? Notes { get; init; }

    /// <summary>The publisher.</summary>
    public string? Publisher { get; init; }

    /// <summary>The publisher's imprint.</summary>
    public string? Imprint { get; init; }

    /// <summary>The credits declared by the source, in source order, as one entry per person and role.</summary>
    public IReadOnlyList<ComicCredit> Credits { get; init; } = [];

    /// <summary>The declared genres.</summary>
    public IReadOnlyList<string> Genres { get; init; } = [];

    /// <summary>Free-form tags.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Characters appearing in the issue.</summary>
    public IReadOnlyList<string> Characters { get; init; } = [];

    /// <summary>Teams appearing in the issue.</summary>
    public IReadOnlyList<string> Teams { get; init; } = [];

    /// <summary>Locations appearing in the issue.</summary>
    public IReadOnlyList<string> Locations { get; init; } = [];

    /// <summary>
    /// Web pages describing the comic. Values that are not absolute URIs are retained in
    /// <see cref="Extensions"/> instead.
    /// </summary>
    public IReadOnlyList<Uri> WebLinks { get; init; } = [];

    /// <summary>External identifiers such as database ids, GTINs or ISBNs.</summary>
    public IReadOnlyList<ComicIdentifier> Identifiers { get; init; } = [];

    /// <summary>The language of this edition, as declared by the source (usually an ISO 639-1 code).</summary>
    public string? Language { get; init; }

    /// <summary>The country of publication, as declared by the source.</summary>
    public string? Country { get; init; }

    /// <summary>The publication year, when declared.</summary>
    public int? Year { get; init; }

    /// <summary>The publication month, when declared.</summary>
    public int? Month { get; init; }

    /// <summary>The publication day, when declared.</summary>
    public int? Day { get; init; }

    /// <summary>
    /// The publication date, available only when <see cref="Year"/>, <see cref="Month"/> and
    /// <see cref="Day"/> form a valid date. Partial dates are available through the individual
    /// components.
    /// </summary>
    public DateOnly? PublicationDate =>
        Year is { } y && Month is { } m && Day is { } d && IsValidDate(y, m, d)
            ? new DateOnly(y, m, d)
            : null;

    /// <summary>The declared age rating. Values are not standardised across tagging applications.</summary>
    public string? AgeRating { get; init; }

    /// <summary>The publication format, such as <c>Annual</c> or <c>Trade Paperback</c>.</summary>
    public string? Format { get; init; }

    /// <summary>Whether the comic is black and white, when declared.</summary>
    public bool? BlackAndWhite { get; init; }

    /// <summary>Whether the comic is manga, and which direction it reads.</summary>
    public MangaReadingDirection Manga { get; init; }

    /// <summary>
    /// The page count declared by the source, which may differ from the number of images in the
    /// archive. <see cref="ComicBook.Pages"/> is authoritative.
    /// </summary>
    public int? DeclaredPageCount { get; init; }

    /// <summary>A community rating, typically between 0 and 5.</summary>
    public double? CommunityRating { get; init; }

    /// <summary>
    /// Source values not mapped to a canonical property, keyed by their name in the source. Includes
    /// standard-specific fields such as <c>StoryArc</c> and fields ComiX does not recognise.
    /// </summary>
    public IReadOnlyDictionary<string, string> Extensions { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private static bool IsValidDate(int year, int month, int day) =>
        year is >= 1 and <= 9999
        && month is >= 1 and <= 12
        && day >= 1
        && day <= DateTime.DaysInMonth(year, month);
}
