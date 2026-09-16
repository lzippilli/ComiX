using System.Globalization;
using System.Xml.Linq;
using ComiX.Internal;

namespace ComiX.Metadata;

/// <summary>
/// Reads the Metron Project's <c>MetronInfo.xml</c> into <see cref="ComicMetadata"/>.
/// </summary>
/// <remarks>
/// MetronInfo nests structured data: a credit is one creator with a list of roles, identifiers carry
/// their source as an attribute, and the series element holds its own attributes and children. The
/// standard defines no per-page information. Elements without a canonical equivalent — stories,
/// arcs, universes, reprints, prices, store date — are retained in
/// <see cref="ComicMetadata.Extensions"/>.
/// </remarks>
internal static class MetronInfoReader
{
    private const string DocumentName = "MetronInfo.xml";

    /// <summary>Parses a <c>MetronInfo.xml</c> document.</summary>
    /// <param name="stream">The document content.</param>
    /// <param name="location">The archive entry the document came from.</param>
    /// <exception cref="ComicMetadataException">The content is not well-formed XML, or is not MetronInfo.</exception>
    public static ComicMetadataDocument Read(Stream stream, string location)
    {
        var root = SecureXml.LoadRoot(stream, DocumentName, "MetronInfo");

        var credits = new List<ComicCredit>();
        var identifiers = new List<ComicIdentifier>();
        var webLinks = new List<Uri>();
        var stories = new List<string>();
        var extensions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var unknownFields = new List<string>();
        var metadata = new ComicMetadata();

        foreach (var element in root.Elements())
        {
            var name = element.Name.LocalName;

            switch (name.ToUpperInvariant())
            {
                case "IDS":
                    identifiers.InsertRange(0, ReadIds(element));
                    break;
                case "PUBLISHER":
                    metadata = metadata with
                    {
                        Publisher = Child(element, "Name"),
                        Imprint = Child(element, "Imprint"),
                    };
                    break;
                case "SERIES":
                    metadata = ApplySeries(metadata, element, extensions);
                    break;
                case "COLLECTIONTITLE":
                    metadata = metadata with { Title = Text(element) };
                    break;
                case "NUMBER":
                    metadata = metadata with { Number = Text(element) };
                    break;
                case "STORIES":
                    stories.AddRange(Children(element, "Story"));
                    break;
                case "SUMMARY":
                    metadata = metadata with { Summary = Text(element) };
                    break;
                case "NOTES":
                    metadata = metadata with { Notes = Text(element) };
                    break;
                case "COVERDATE":
                    metadata = ApplyDate(metadata, Text(element));
                    break;
                case "PAGECOUNT":
                    // The schema default is 0, which denotes an undeclared count.
                    metadata = metadata with { DeclaredPageCount = ParsePositive(Text(element)) };
                    break;
                case "GENRES":
                    metadata = metadata with { Genres = Children(element, "Genre") };
                    break;
                case "TAGS":
                    metadata = metadata with { Tags = Children(element, "Tag") };
                    break;
                case "CHARACTERS":
                    metadata = metadata with { Characters = Children(element, "Character") };
                    break;
                case "TEAMS":
                    metadata = metadata with { Teams = Children(element, "Team") };
                    break;
                case "LOCATIONS":
                    metadata = metadata with { Locations = Children(element, "Location") };
                    break;
                case "GTIN":
                    identifiers.AddRange(ReadGtin(element));
                    break;
                case "AGERATING":
                    // The schema default is Unknown, which denotes an undeclared rating.
                    var rating = Text(element);
                    if (rating is not null && !rating.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
                    {
                        metadata = metadata with { AgeRating = rating };
                    }

                    break;
                case "URLS":
                    ReadUrls(element, webLinks, extensions);
                    break;
                case "CREDITS":
                    credits.AddRange(ReadCredits(element));
                    break;
                case "MANGAVOLUME":
                case "STOREDATE":
                case "LASTMODIFIED":
                    Preserve(extensions, name, Text(element));
                    break;
                case "ARCS":
                    Preserve(extensions, name, Join(element.Elements().Select(arc =>
                        Child(arc, "Number") is { } number
                            ? $"{Child(arc, "Name")} #{number}"
                            : Child(arc, "Name"))));
                    break;
                case "UNIVERSES":
                    Preserve(extensions, name, Join(element.Elements().Select(universe =>
                        Child(universe, "Designation") is { } designation
                            ? $"{Child(universe, "Name")} ({designation})"
                            : Child(universe, "Name"))));
                    break;
                case "REPRINTS":
                    Preserve(extensions, name, Join(Children(element, "Reprint")));
                    break;
                case "PRICES":
                    Preserve(extensions, name, Join(element.Elements().Select(price =>
                        price.Attribute("country")?.Value is { Length: > 0 } country
                            ? $"{Text(price)} {country}"
                            : Text(price))));
                    break;
                default:
                    unknownFields.Add(name);
                    Preserve(extensions, name, Text(element));
                    break;
            }
        }

        // MetronInfo has no issue title. A collection title is used when present; otherwise a
        // single story title identifies the issue. Every story is retained regardless.
        if (metadata.Title is null && stories.Count == 1)
        {
            metadata = metadata with { Title = stories[0] };
        }

        if (stories.Count > 0)
        {
            extensions["Stories"] = string.Join(", ", stories);
        }

        metadata = metadata with
        {
            Credits = credits,
            Identifiers = identifiers,
            WebLinks = webLinks,
            Extensions = extensions,
            Sources = [new ComicMetadataSource(ComicMetadataStandard.MetronInfo, location, IsPrimary: true)],
        };

        return new ComicMetadataDocument(metadata, [], unknownFields);
    }

    private static ComicMetadata ApplySeries(
        ComicMetadata metadata,
        XElement series,
        Dictionary<string, string> extensions)
    {
        Preserve(extensions, "Series.StartYear", Child(series, "StartYear"));
        Preserve(extensions, "Series.VolumeCount", Child(series, "VolumeCount"));

        var alternativeNames = series.Elements()
            .FirstOrDefault(child => child.Name.LocalName.Equals("AlternativeNames", StringComparison.OrdinalIgnoreCase));
        if (alternativeNames is not null)
        {
            Preserve(extensions, "Series.AlternativeNames", Join(Children(alternativeNames, "AlternativeName")));
        }

        return metadata with
        {
            Series = Child(series, "Name"),
            SeriesSort = Child(series, "SortName"),
            Volume = Child(series, "Volume"),
            Format = Child(series, "Format"),
            Count = ParsePositive(Child(series, "IssueCount")),
            Language = series.Attribute("lang")?.Value is { Length: > 0 } lang ? lang : null,
        };
    }

    /// <summary>
    /// Reads external identifiers. The primary identifier is placed first; the source name is
    /// lower-cased without spaces, matching the convention documented on <see cref="ComicIdentifier"/>.
    /// </summary>
    private static IEnumerable<ComicIdentifier> ReadIds(XElement ids)
    {
        var ordered = ids.Elements()
            .Where(id => id.Name.LocalName.Equals("ID", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(id => IsTrue(id.Attribute("primary")?.Value));

        foreach (var element in ordered)
        {
            var source = element.Attribute("source")?.Value;
            var value = Text(element);
            if (string.IsNullOrEmpty(source) || value is null)
            {
                continue;
            }

            yield return new ComicIdentifier(NormaliseSource(source), value);
        }
    }

    private static IEnumerable<ComicIdentifier> ReadGtin(XElement gtin)
    {
        if (Child(gtin, "ISBN") is { } isbn)
        {
            yield return new ComicIdentifier("isbn", isbn);
        }

        if (Child(gtin, "UPC") is { } upc)
        {
            yield return new ComicIdentifier("upc", upc);
        }
    }

    private static void ReadUrls(XElement urls, List<Uri> webLinks, Dictionary<string, string> extensions)
    {
        var ordered = urls.Elements()
            .Where(url => url.Name.LocalName.Equals("URL", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(url => IsTrue(url.Attribute("primary")?.Value));

        var unparsed = new List<string>();
        foreach (var url in ordered)
        {
            var value = Text(url);
            if (value is null)
            {
                continue;
            }

            if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
            {
                webLinks.Add(uri);
            }
            else
            {
                unparsed.Add(value);
            }
        }

        if (unparsed.Count > 0)
        {
            extensions["URLs"] = string.Join(", ", unparsed);
        }
    }

    /// <summary>
    /// Expands each credit into one entry per role. A credit without roles is retained with
    /// <see cref="ComicCreditRole.Unknown"/> rather than discarded.
    /// </summary>
    private static IEnumerable<ComicCredit> ReadCredits(XElement credits)
    {
        foreach (var credit in credits.Elements())
        {
            var creator = Child(credit, "Creator");
            if (creator is null)
            {
                continue;
            }

            var roles = credit.Elements()
                .FirstOrDefault(child => child.Name.LocalName.Equals("Roles", StringComparison.OrdinalIgnoreCase));
            var roleNames = roles is null ? [] : Children(roles, "Role");

            if (roleNames.Count == 0)
            {
                yield return new ComicCredit(creator, ComicCreditRole.Unknown);
                continue;
            }

            foreach (var role in roleNames)
            {
                yield return new ComicCredit(creator, ComicCreditRole.Parse(role));
            }
        }
    }

    /// <summary>MetronInfo dates are complete ISO 8601 dates.</summary>
    private static ComicMetadata ApplyDate(ComicMetadata metadata, string? value)
    {
        if (value is null
            || !DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return metadata;
        }

        return metadata with { Year = date.Year, Month = date.Month, Day = date.Day };
    }

    private static string? Text(XElement element) =>
        element.Value.Trim() is { Length: > 0 } value ? value : null;

    private static string? Child(XElement parent, string name) =>
        parent.Elements()
            .FirstOrDefault(child => child.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))
            is { } child
            ? Text(child)
            : null;

    private static List<string> Children(XElement parent, string name) =>
        parent.Elements()
            .Where(child => child.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))
            .Select(Text)
            .OfType<string>()
            .ToList();

    private static string? Join(IEnumerable<string?> values) =>
        string.Join(", ", values.OfType<string>()) is { Length: > 0 } joined ? joined : null;

    private static void Preserve(Dictionary<string, string> extensions, string key, string? value)
    {
        if (value is not null)
        {
            extensions[key] = value;
        }
    }

    private static int? ParsePositive(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
            ? parsed
            : null;

    private static bool IsTrue(string? value) =>
        value is not null && (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1");

    private static string NormaliseSource(string source) =>
        source.Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
}
