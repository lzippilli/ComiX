using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace ComiX.Metadata;

/// <summary>
/// Reads ComicRack's <c>ComicInfo.xml</c> into <see cref="ComicMetadata"/>.
/// </summary>
/// <remarks>
/// The document is parsed with DTD processing disabled and no entity resolver, as archive content is
/// untrusted input. Fields specific to this standard, and fields with no canonical mapping, are
/// retained in <see cref="ComicMetadata.Extensions"/> under their element name.
/// </remarks>
internal static class ComicInfoReader
{
    private static readonly char[] ListSeparators = [','];

    /// <summary>The ComicInfo elements that hold credits, mapped to the role they mean.</summary>
    private static readonly Dictionary<string, ComicCreditRole> CreditRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Writer"] = ComicCreditRole.Writer,
        ["Penciller"] = ComicCreditRole.Penciller,
        ["Inker"] = ComicCreditRole.Inker,
        ["Colorist"] = ComicCreditRole.Colorist,
        ["Letterer"] = ComicCreditRole.Letterer,
        ["CoverArtist"] = ComicCreditRole.CoverArtist,
        ["Editor"] = ComicCreditRole.Editor,
        ["Translator"] = ComicCreditRole.Translator,
    };

    /// <summary>
    /// ComicInfo fields retained in <see cref="ComicMetadata.Extensions"/> rather than promoted to a
    /// canonical property, as their meaning is specific to this standard.
    /// </summary>
    private static readonly HashSet<string> PreservedFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "AlternateCount",
        "StoryArc",
        "StoryArcNumber",
        "SeriesGroup",
        "ScanInformation",
        "MainCharacterOrTeam",
        "Review",
    };

    /// <summary>
    /// Parses a <c>ComicInfo.xml</c> document.
    /// </summary>
    /// <param name="stream">The document's content.</param>
    /// <param name="location">The archive entry the document came from, recorded on the metadata source.</param>
    /// <exception cref="ComicMetadataException">The content is not well-formed XML, or is not ComicInfo.</exception>
    public static ComicMetadataDocument Read(Stream stream, string location)
    {
        XDocument document;
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
                IgnoreWhitespace = true,
                CloseInput = false,
            };

            using var reader = XmlReader.Create(stream, settings);
            document = XDocument.Load(reader);
        }
        catch (Exception ex) when (ex is XmlException or InvalidOperationException)
        {
            throw new ComicMetadataException("ComicInfo.xml is not well-formed XML.", ex);
        }

        var root = document.Root
            ?? throw new ComicMetadataException("ComicInfo.xml is empty.");

        if (!root.Name.LocalName.Equals("ComicInfo", StringComparison.OrdinalIgnoreCase))
        {
            throw new ComicMetadataException(
                $"Expected a ComicInfo root element but found '{root.Name.LocalName}'.");
        }

        var credits = new List<ComicCredit>();
        var identifiers = new List<ComicIdentifier>();
        var webLinks = new List<Uri>();
        var extensions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var unknownFields = new List<string>();
        var pages = new List<PageMetadata>();
        var metadata = new ComicMetadata();

        foreach (var element in root.Elements())
        {
            var name = element.Name.LocalName;

            if (name.Equals("Pages", StringComparison.OrdinalIgnoreCase))
            {
                pages.AddRange(ReadPages(element));
                continue;
            }

            var value = element.Value.Trim();
            if (value.Length == 0)
            {
                continue;
            }

            if (CreditRoles.TryGetValue(name, out var role))
            {
                credits.AddRange(SplitList(value).Select(person => new ComicCredit(person, role)));
                continue;
            }

            switch (name.ToUpperInvariant())
            {
                case "TITLE":
                    metadata = metadata with { Title = value };
                    break;
                case "SERIES":
                    metadata = metadata with { Series = value };
                    break;
                case "NUMBER":
                    metadata = metadata with { Number = value };
                    break;
                case "COUNT":
                    metadata = metadata with { Count = ParseInt(value) };
                    break;
                case "VOLUME":
                    metadata = metadata with { Volume = value };
                    break;
                case "ALTERNATESERIES":
                    metadata = metadata with { AlternateSeries = value };
                    break;
                case "ALTERNATENUMBER":
                    metadata = metadata with { AlternateNumber = value };
                    break;
                case "SUMMARY":
                    metadata = metadata with { Summary = value };
                    break;
                case "NOTES":
                    metadata = metadata with { Notes = value };
                    break;
                case "PUBLISHER":
                    metadata = metadata with { Publisher = value };
                    break;
                case "IMPRINT":
                    metadata = metadata with { Imprint = value };
                    break;
                case "GENRE":
                    metadata = metadata with { Genres = SplitList(value) };
                    break;
                case "TAGS":
                    metadata = metadata with { Tags = SplitList(value) };
                    break;
                case "CHARACTERS":
                    metadata = metadata with { Characters = SplitList(value) };
                    break;
                case "TEAMS":
                    metadata = metadata with { Teams = SplitList(value) };
                    break;
                case "LOCATIONS":
                    metadata = metadata with { Locations = SplitList(value) };
                    break;
                case "LANGUAGEISO":
                    metadata = metadata with { Language = value };
                    break;
                case "COUNTRY":
                    metadata = metadata with { Country = value };
                    break;
                case "YEAR":
                    metadata = metadata with { Year = ParseInt(value) };
                    break;
                case "MONTH":
                    metadata = metadata with { Month = ParseInt(value) };
                    break;
                case "DAY":
                    metadata = metadata with { Day = ParseInt(value) };
                    break;
                case "AGERATING":
                    metadata = metadata with { AgeRating = value };
                    break;
                case "FORMAT":
                    metadata = metadata with { Format = value };
                    break;
                case "BLACKANDWHITE":
                    metadata = metadata with { BlackAndWhite = ParseYesNo(value) };
                    break;
                case "MANGA":
                    metadata = metadata with { Manga = ParseManga(value) };
                    break;
                case "PAGECOUNT":
                    metadata = metadata with { DeclaredPageCount = ParseInt(value) };
                    break;
                case "COMMUNITYRATING":
                    metadata = metadata with { CommunityRating = ParseDouble(value) };
                    break;
                case "WEB":
                    AddWebLinks(value, webLinks, extensions);
                    break;
                case "GTIN":
                    identifiers.Add(new ComicIdentifier("gtin", value));
                    break;
                default:
                    if (!PreservedFields.Contains(name))
                    {
                        unknownFields.Add(name);
                    }

                    break;
            }

            // Anything not promoted to a canonical field is preserved rather than dropped.
            if (!IsMapped(name))
            {
                extensions[name] = value;
            }
        }

        metadata = metadata with
        {
            Credits = credits,
            Identifiers = identifiers,
            WebLinks = webLinks,
            Extensions = extensions,
            Sources = [new ComicMetadataSource(ComicMetadataStandard.ComicInfo, location, IsPrimary: true)],
        };

        return new ComicMetadataDocument(metadata, pages, unknownFields);
    }

    private static bool IsMapped(string name) =>
        CreditRoles.ContainsKey(name)
        || name.ToUpperInvariant() is "TITLE" or "SERIES" or "NUMBER" or "COUNT" or "VOLUME"
            or "ALTERNATESERIES" or "ALTERNATENUMBER" or "SUMMARY" or "NOTES" or "PUBLISHER"
            or "IMPRINT" or "GENRE" or "TAGS" or "CHARACTERS" or "TEAMS" or "LOCATIONS"
            or "LANGUAGEISO" or "COUNTRY" or "AGERATING" or "FORMAT" or "BLACKANDWHITE" or "MANGA"
            or "PAGECOUNT" or "COMMUNITYRATING" or "WEB" or "GTIN" or "PAGES";

    private static IEnumerable<PageMetadata> ReadPages(XElement pagesElement)
    {
        foreach (var page in pagesElement.Elements())
        {
            if (!page.Name.LocalName.Equals("Page", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var image = ParseInt(Attribute(page, "Image"));
            if (image is null)
            {
                continue;
            }

            yield return new PageMetadata(
                image.Value,
                Attribute(page, "Key"),
                ParsePageType(Attribute(page, "Type")),
                ParseSpread(Attribute(page, "DoublePage")),
                ParseDimension(Attribute(page, "ImageWidth")),
                ParseDimension(Attribute(page, "ImageHeight")),
                Attribute(page, "Bookmark"));
        }
    }

    private static string? Attribute(XElement element, string name) =>
        element.Attributes()
            .FirstOrDefault(a => a.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?.Value.Trim() is { Length: > 0 } value
            ? value
            : null;

    private static void AddWebLinks(string value, List<Uri> webLinks, Dictionary<string, string> extensions)
    {
        var any = false;
        foreach (var candidate in value.Split([' ', ',', ';'], StringSplitOptions.RemoveEmptyEntries
                     | StringSplitOptions.TrimEntries))
        {
            if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
            {
                webLinks.Add(uri);
                any = true;
            }
        }

        if (!any)
        {
            extensions["Web"] = value;
        }
    }

    private static List<string> SplitList(string value) =>
        [.. value.Split(ListSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    private static int? ParseInt(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    private static double? ParseDouble(string? value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

    /// <summary>
    /// The ComicInfo schema defines a default of <c>false</c> for <c>DoublePage</c>. An absent
    /// attribute is mapped to <see cref="ComicPageSpread.Unknown"/> rather than to that default,
    /// which would assert a layout the source did not declare.
    /// </summary>
    private static ComicPageSpread ParseSpread(string? value) =>
        ParseYesNo(value) switch
        {
            true => ComicPageSpread.Double,
            false => ComicPageSpread.Single,
            null => ComicPageSpread.Unknown,
        };

    /// <summary>
    /// ComicInfo uses <c>-1</c> as the schema default for <c>ImageWidth</c> and <c>ImageHeight</c>,
    /// meaning "not measured". Non-positive values are therefore mapped to <see langword="null"/>.
    /// </summary>
    private static int? ParseDimension(string? value) =>
        ParseInt(value) is { } parsed && parsed > 0 ? parsed : null;

    private static bool? ParseYesNo(string? value) =>
        value?.Trim().ToUpperInvariant() switch
        {
            "YES" or "TRUE" => true,
            "NO" or "FALSE" => false,
            _ => null,
        };

    private static MangaReadingDirection ParseManga(string value) =>
        value.Trim().ToUpperInvariant() switch
        {
            "YES" => MangaReadingDirection.Yes,
            "YESANDRIGHTTOLEFT" => MangaReadingDirection.YesRightToLeft,
            "NO" => MangaReadingDirection.No,
            _ => MangaReadingDirection.Unknown,
        };

    private static ComicPageType ParsePageType(string? value) =>
        value?.Trim().ToUpperInvariant() switch
        {
            "FRONTCOVER" => ComicPageType.FrontCover,
            "INNERCOVER" => ComicPageType.InnerCover,
            "ROUNDUP" => ComicPageType.Roundup,
            "STORY" => ComicPageType.Story,
            "ADVERTISEMENT" => ComicPageType.Advertisement,
            "EDITORIAL" => ComicPageType.Editorial,
            "LETTERS" => ComicPageType.Letters,
            "PREVIEW" => ComicPageType.Preview,
            "BACKCOVER" => ComicPageType.BackCover,
            "OTHER" => ComicPageType.Other,
            "DELETED" => ComicPageType.Deleted,
            _ => ComicPageType.Unknown,
        };
}
