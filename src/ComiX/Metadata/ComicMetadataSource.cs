namespace ComiX;

/// <summary>
/// A comic metadata standard ComiX can read.
/// </summary>
public enum ComicMetadataStandard
{
    /// <summary>No metadata standard.</summary>
    None = 0,

    /// <summary>ComicRack's <c>ComicInfo.xml</c>.</summary>
    ComicInfo = 1,

    /// <summary>The CoMet XML standard.</summary>
    CoMet = 2,

    /// <summary>ComicBookInfo (Comic Book Lover), stored as a JSON archive comment.</summary>
    ComicBookInfo = 3,

    /// <summary>The Metron Project's <c>MetronInfo.xml</c>.</summary>
    MetronInfo = 4,
}

/// <summary>
/// One metadata source found in an archive.
/// </summary>
/// <param name="Standard">The standard this source follows.</param>
/// <param name="Location">
/// The archive entry name, or a description of the location for sources not stored as an entry.
/// </param>
/// <param name="IsPrimary">
/// Whether this source supplied the canonical values. At most one source in an archive is primary;
/// the others are listed without contributing values.
/// </param>
public sealed record ComicMetadataSource(ComicMetadataStandard Standard, string Location, bool IsPrimary);
