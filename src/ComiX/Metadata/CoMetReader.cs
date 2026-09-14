using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace ComiX.Metadata;

/// <summary>
/// Reads the CoMet standard into <see cref="ComicMetadata"/>.
/// </summary>
/// <remarks>
/// CoMet repeats elements instead of using delimited lists, so multiple genres or credits appear as
/// multiple elements. The document is parsed with the same hardened XML settings as ComicInfo. The
/// standard defines no per-page information.
/// </remarks>
internal static class CoMetReader
{
    private static readonly Dictionary<string, ComicCreditRole> CreditElements =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["writer"] = ComicCreditRole.Writer,
            ["penciller"] = ComicCreditRole.Penciller,
            ["inker"] = ComicCreditRole.Inker,
            ["colorist"] = ComicCreditRole.Colorist,
            ["letterer"] = ComicCreditRole.Letterer,
            ["coverDesigner"] = ComicCreditRole.CoverArtist,
            ["editor"] = ComicCreditRole.Editor,
            ["creator"] = ComicCreditRole.Custom("Creator"),
        };

    /// <summary>Fields CoMet defines that have no canonical equivalent.</summary>
    private static readonly HashSet<string> PreservedFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "rights",
        "price",
        "isVersionOf",
        "coverImage",
        "lastMark",
        "readingDirection",
    };

    /// <summary>Parses a CoMet document.</summary>
    /// <param name="stream">The document's content.</param>
    /// <param name="location">The archive entry the document came from.</param>
    /// <exception cref="ComicMetadataException">The content is not well-formed XML, or is not CoMet.</exception>
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
            throw new ComicMetadataException("The CoMet document is not well-formed XML.", ex);
        }

        var root = document.Root
            ?? throw new ComicMetadataException("The CoMet document is empty.");

        if (!root.Name.LocalName.Equals("comet", StringComparison.OrdinalIgnoreCase))
        {
            throw new ComicMetadataException(
                $"Expected a comet root element but found '{root.Name.LocalName}'.");
        }

        var credits = new List<ComicCredit>();
        var genres = new List<string>();
        var characters = new List<string>();
        var identifiers = new List<ComicIdentifier>();
        var extensions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var unknownFields = new List<string>();
        var metadata = new ComicMetadata();

        foreach (var element in root.Elements())
        {
            var name = element.Name.LocalName;
            var value = element.Value.Trim();
            if (value.Length == 0)
            {
                continue;
            }

            if (CreditElements.TryGetValue(name, out var role))
            {
                credits.Add(new ComicCredit(value, role));
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
                case "ISSUE":
                    metadata = metadata with { Number = value };
                    break;
                case "VOLUME":
                    metadata = metadata with { Volume = value };
                    break;
                case "PUBLISHER":
                    metadata = metadata with { Publisher = value };
                    break;
                case "DESCRIPTION":
                    metadata = metadata with { Summary = value };
                    break;
                case "GENRE":
                    genres.Add(value);
                    break;
                case "CHARACTER":
                    characters.Add(value);
                    break;
                case "LANGUAGE":
                    metadata = metadata with { Language = value };
                    break;
                case "FORMAT":
                    metadata = metadata with { Format = value };
                    break;
                case "RATING":
                    metadata = metadata with { AgeRating = value };
                    break;
                case "PAGES":
                    metadata = metadata with { DeclaredPageCount = ParseInt(value) };
                    break;
                case "IDENTIFIER":
                    identifiers.Add(new ComicIdentifier("comet", value));
                    break;
                case "DATE":
                    metadata = ApplyDate(metadata, value);
                    break;
                case "READINGDIRECTION":
                    // "rtl" is how CoMet expresses what ComicInfo calls right-to-left manga. "ltr"
                    // says nothing about whether a comic is manga, so it is not mapped.
                    if (value.Equals("rtl", StringComparison.OrdinalIgnoreCase))
                    {
                        metadata = metadata with { Manga = MangaReadingDirection.YesRightToLeft };
                    }

                    break;
                default:
                    if (!PreservedFields.Contains(name))
                    {
                        unknownFields.Add(name);
                    }

                    break;
            }

            if (!IsMapped(name))
            {
                extensions[name] = value;
            }
        }

        metadata = metadata with
        {
            Credits = credits,
            Genres = genres,
            Characters = characters,
            Identifiers = identifiers,
            Extensions = extensions,
            Sources = [new ComicMetadataSource(ComicMetadataStandard.CoMet, location, IsPrimary: true)],
        };

        return new ComicMetadataDocument(metadata, [], unknownFields);
    }

    private static bool IsMapped(string name) =>
        CreditElements.ContainsKey(name)
        || name.ToUpperInvariant() is "TITLE" or "SERIES" or "ISSUE" or "VOLUME" or "PUBLISHER"
            or "DESCRIPTION" or "GENRE" or "CHARACTER" or "LANGUAGE" or "FORMAT" or "RATING"
            or "PAGES" or "IDENTIFIER" or "DATE";

    /// <summary>CoMet dates are ISO-8601, and are often partial (<c>1988</c> or <c>1988-03</c>).</summary>
    private static ComicMetadata ApplyDate(ComicMetadata metadata, string value)
    {
        var parts = value.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return metadata with
        {
            Year = parts.Length > 0 ? ParseInt(parts[0]) : null,
            Month = parts.Length > 1 ? ParseInt(parts[1]) : null,
            Day = parts.Length > 2 ? ParseInt(parts[2]) : null,
        };
    }

    private static int? ParseInt(string? value) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
}
