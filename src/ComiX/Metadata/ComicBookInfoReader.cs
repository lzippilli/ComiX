using System.Globalization;
using System.Text.Json;

namespace ComiX.Metadata;

/// <summary>
/// Reads the ComicBookInfo standard, which stores JSON in the archive's comment rather than in a file.
/// </summary>
/// <remarks>
/// <para>
/// The standard is available only in container formats that define an archive comment, namely ZIP
/// and RAR, and is preserved by a conversion only if the comment is carried across.
/// </para>
/// <para>
/// The payload is an object under a versioned key, <c>ComicBookInfo/1.0</c>. Fields outside that key,
/// such as <c>appID</c> and <c>lastModified</c>, describe the tagging application rather than the
/// comic and are not promoted to canonical properties.
/// </para>
/// </remarks>
internal static class ComicBookInfoReader
{
    private const string KeyPrefix = "ComicBookInfo/";

    /// <summary>Whether a comment could be a ComicBookInfo payload, without parsing it.</summary>
    public static bool LooksLikeComicBookInfo(string? comment) =>
        comment is not null
        && comment.AsSpan().TrimStart().StartsWith("{", StringComparison.Ordinal)
        && comment.Contains(KeyPrefix, StringComparison.Ordinal);

    /// <summary>Parses a ComicBookInfo comment.</summary>
    /// <param name="comment">The archive comment.</param>
    /// <param name="location">A description of where the comment came from.</param>
    /// <exception cref="ComicMetadataException">The comment is not valid ComicBookInfo JSON.</exception>
    public static ComicMetadataDocument Read(string comment, string location)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(comment);
        }
        catch (JsonException ex)
        {
            throw new ComicMetadataException("The ComicBookInfo comment is not valid JSON.", ex);
        }

        using (document)
        {
            if (document.RootElement.ValueKind is not JsonValueKind.Object)
            {
                throw new ComicMetadataException("The ComicBookInfo comment is not a JSON object.");
            }

            var payload = FindPayload(document.RootElement)
                ?? throw new ComicMetadataException(
                    "The comment has no ComicBookInfo section.");

            return ReadPayload(payload, location);
        }
    }

    private static JsonElement? FindPayload(JsonElement root)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (property.Name.StartsWith(KeyPrefix, StringComparison.OrdinalIgnoreCase)
                && property.Value.ValueKind is JsonValueKind.Object)
            {
                return property.Value;
            }
        }

        return null;
    }

    private static ComicMetadataDocument ReadPayload(JsonElement payload, string location)
    {
        var credits = new List<ComicCredit>();
        var genres = new List<string>();
        var tags = new List<string>();
        var extensions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var unknownFields = new List<string>();
        var metadata = new ComicMetadata();

        foreach (var property in payload.EnumerateObject())
        {
            switch (property.Name.ToUpperInvariant())
            {
                case "SERIES":
                    metadata = metadata with { Series = Text(property.Value) };
                    break;
                case "TITLE":
                    metadata = metadata with { Title = Text(property.Value) };
                    break;
                case "PUBLISHER":
                    metadata = metadata with { Publisher = Text(property.Value) };
                    break;
                case "ISSUE":
                    metadata = metadata with { Number = Text(property.Value) };
                    break;
                case "NUMBEROFISSUES":
                    metadata = metadata with { Count = Number(property.Value) };
                    break;
                case "VOLUME":
                    metadata = metadata with { Volume = Text(property.Value) };
                    break;
                case "PUBLICATIONYEAR":
                    metadata = metadata with { Year = Number(property.Value) };
                    break;
                case "PUBLICATIONMONTH":
                    metadata = metadata with { Month = Number(property.Value) };
                    break;
                case "LANGUAGE":
                    metadata = metadata with { Language = Text(property.Value) };
                    break;
                case "COUNTRY":
                    metadata = metadata with { Country = Text(property.Value) };
                    break;
                case "RATING":
                    metadata = metadata with { CommunityRating = Fraction(property.Value) };
                    break;
                case "COMMENTS":
                    // ComicBookInfo's "comments" is the synopsis, which is what taggers put there.
                    metadata = metadata with { Summary = Text(property.Value) };
                    break;
                case "GENRE":
                    genres.AddRange(Strings(property.Value));
                    break;
                case "TAGS":
                    tags.AddRange(Strings(property.Value));
                    break;
                case "CREDITS":
                    credits.AddRange(ReadCredits(property.Value));
                    break;
                case "NUMBEROFVOLUMES":
                    Preserve(extensions, property);
                    break;
                default:
                    unknownFields.Add(property.Name);
                    Preserve(extensions, property);
                    break;
            }
        }

        metadata = metadata with
        {
            Credits = credits,
            Genres = genres,
            Tags = tags,
            Extensions = extensions,
            Sources = [new ComicMetadataSource(ComicMetadataStandard.ComicBookInfo, location, IsPrimary: true)],
        };

        return new ComicMetadataDocument(metadata, [], unknownFields);
    }

    private static IEnumerable<ComicCredit> ReadCredits(JsonElement credits)
    {
        if (credits.ValueKind is not JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var credit in credits.EnumerateArray())
        {
            if (credit.ValueKind is not JsonValueKind.Object)
            {
                continue;
            }

            var person = credit.TryGetProperty("person", out var personValue) ? Text(personValue) : null;
            if (person is null)
            {
                continue;
            }

            var role = credit.TryGetProperty("role", out var roleValue) ? Text(roleValue) : null;
            yield return new ComicCredit(
                person,
                role is null ? ComicCreditRole.Unknown : ComicCreditRole.Parse(role));
        }
    }

    private static void Preserve(Dictionary<string, string> extensions, JsonProperty property)
    {
        var value = property.Value.ValueKind switch
        {
            JsonValueKind.String => property.Value.GetString(),
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => property.Value.GetRawText(),
        };

        if (!string.IsNullOrWhiteSpace(value))
        {
            extensions[property.Name] = value;
        }
    }

    private static string? Text(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() is { Length: > 0 } text ? text : null,
            JsonValueKind.Number => value.ToString(),
            _ => null,
        };

    private static int? Number(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(
                value.GetString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var parsed) => parsed,
            _ => null,
        };

    private static double? Fraction(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDouble(out var number) => number,
            JsonValueKind.String when double.TryParse(
                value.GetString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsed) => parsed,
            _ => null,
        };

    private static IEnumerable<string> Strings(JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var item in value.EnumerateArray())
                {
                    if (Text(item) is { } text)
                    {
                        yield return text;
                    }
                }

                break;

            case JsonValueKind.String:
                // Some taggers write a single delimited string where the standard says array.
                foreach (var part in value.GetString()!.Split(
                             ',',
                             StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    yield return part;
                }

                break;
        }
    }
}
