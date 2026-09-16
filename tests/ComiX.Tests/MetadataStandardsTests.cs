using System.Text;
using ComiX.Tests.Fixtures;
using Xunit;

namespace ComiX.Tests;

/// <summary>
/// The standards other than ComicInfo, and what happens when a comic carries more than one.
/// </summary>
public sealed class MetadataStandardsTests : IDisposable
{
    private const string CoMet = """
        <?xml version="1.0" encoding="utf-8"?>
        <comet xmlns="http://www.denvog.com/comet/">
          <title>Guardian Devil</title>
          <series>Daredevil</series>
          <issue>1</issue>
          <volume>2</volume>
          <publisher>Marvel</publisher>
          <date>1998-11-04</date>
          <genre>Superhero</genre>
          <genre>Crime</genre>
          <character>Daredevil</character>
          <character>Karen Page</character>
          <writer>Kevin Smith</writer>
          <penciller>Joe Quesada</penciller>
          <coverDesigner>Joe Quesada</coverDesigner>
          <description>A synopsis.</description>
          <language>en</language>
          <pages>3</pages>
          <rating>Teen</rating>
          <identifier>urn:isbn:9780785134794</identifier>
          <rights>Copyright Marvel</rights>
          <readingDirection>rtl</readingDirection>
        </comet>
        """;

    private const string ComicBookInfo = """
        {
          "appID": "ComiX/1.0",
          "lastModified": "2026-01-01 00:00:00 +0000",
          "ComicBookInfo/1.0": {
            "series": "Daredevil",
            "title": "Guardian Devil",
            "publisher": "Marvel",
            "issue": 1,
            "numberOfIssues": 8,
            "volume": 2,
            "publicationYear": 1998,
            "publicationMonth": 11,
            "language": "English",
            "country": "US",
            "rating": 4.5,
            "genre": "Superhero, Crime",
            "tags": ["classic", "noir"],
            "comments": "A synopsis.",
            "credits": [
              { "person": "Kevin Smith", "role": "Writer", "primary": true },
              { "person": "Joe Quesada", "role": "Pencils" }
            ]
          }
        }
        """;

    private const string MetronInfo = """
        <?xml version="1.0" encoding="utf-8"?>
        <MetronInfo xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
          <IDS>
            <ID source="Comic Vine">12345</ID>
            <ID source="Metron" primary="true">678</ID>
            <ID source="Grand Comics Database">91011</ID>
          </IDS>
          <Publisher id="1">
            <Name>Marvel</Name>
            <Imprint>Marvel Knights</Imprint>
          </Publisher>
          <Series lang="en" id="42">
            <Name>Daredevil</Name>
            <SortName>Daredevil</SortName>
            <Volume>2</Volume>
            <Format>Single Issue</Format>
            <StartYear>1998</StartYear>
            <IssueCount>119</IssueCount>
            <AlternativeNames>
              <AlternativeName lang="es">Dan Defensor</AlternativeName>
            </AlternativeNames>
          </Series>
          <Number>1</Number>
          <Stories>
            <Story>Guardian Devil, Part One</Story>
          </Stories>
          <Summary>A synopsis.</Summary>
          <Notes>Tagged with Metron Tagger.</Notes>
          <Prices>
            <Price country="US">2.99</Price>
          </Prices>
          <CoverDate>1998-11-01</CoverDate>
          <StoreDate>1998-09-16</StoreDate>
          <PageCount>3</PageCount>
          <Genres>
            <Genre id="1">Superhero</Genre>
            <Genre>Crime</Genre>
          </Genres>
          <Tags>
            <Tag>classic</Tag>
          </Tags>
          <Arcs>
            <Arc id="7">
              <Name>Guardian Devil</Name>
              <Number>1</Number>
            </Arc>
          </Arcs>
          <Characters>
            <Character>Daredevil</Character>
            <Character>Karen Page</Character>
          </Characters>
          <Teams>
            <Team>The Defenders</Team>
          </Teams>
          <Locations>
            <Location>Hell's Kitchen</Location>
          </Locations>
          <GTIN>
            <ISBN>9780785134794</ISBN>
            <UPC>759606044313</UPC>
          </GTIN>
          <AgeRating>Teen</AgeRating>
          <URLs>
            <URL>https://comicvine.gamespot.com/daredevil-1/4000-12345/</URL>
            <URL primary="true">https://metron.cloud/issue/daredevil-1998-1/</URL>
          </URLs>
          <Credits>
            <Credit>
              <Creator id="10">Kevin Smith</Creator>
              <Roles>
                <Role id="1">Writer</Role>
              </Roles>
            </Credit>
            <Credit>
              <Creator id="11">Joe Quesada</Creator>
              <Roles>
                <Role>Penciller</Role>
                <Role>Cover</Role>
              </Roles>
            </Credit>
            <Credit>
              <Creator id="12">Jimmy Palmiotti</Creator>
              <Roles>
                <Role>Inker</Role>
                <Role>Ink Assists</Role>
              </Roles>
            </Credit>
          </Credits>
          <LastModified>2026-01-01T00:00:00</LastModified>
        </MetronInfo>
        """;

    private readonly ComicFixture _fixture = new();

    [Fact]
    public async Task Reads_metroninfo_into_the_common_model()
    {
        var path = CreateComic(metronInfo: MetronInfo);

        await using var comic = await ComicBook.OpenAsync(path);
        var metadata = comic.Metadata;

        Assert.Equal(ComicMetadataStandard.MetronInfo, metadata.Standard);
        Assert.Equal("MetronInfo.xml", metadata.PrimarySource!.Location);
        Assert.Equal("Daredevil", metadata.Series);
        Assert.Equal("Daredevil", metadata.SeriesSort);
        Assert.Equal("2", metadata.Volume);
        Assert.Equal("Single Issue", metadata.Format);
        Assert.Equal(119, metadata.Count);
        Assert.Equal("en", metadata.Language);
        Assert.Equal("1", metadata.Number);
        Assert.Equal("Marvel", metadata.Publisher);
        Assert.Equal("Marvel Knights", metadata.Imprint);
        Assert.Equal("A synopsis.", metadata.Summary);
        Assert.Equal("Tagged with Metron Tagger.", metadata.Notes);
        Assert.Equal(new DateOnly(1998, 11, 1), metadata.PublicationDate);
        Assert.Equal(3, metadata.DeclaredPageCount);
        Assert.Equal("Teen", metadata.AgeRating);
    }

    [Fact]
    public async Task Reads_metroninfo_lists()
    {
        var path = CreateComic(metronInfo: MetronInfo);

        await using var comic = await ComicBook.OpenAsync(path);
        var metadata = comic.Metadata;

        Assert.Equal(["Superhero", "Crime"], metadata.Genres);
        Assert.Equal(["classic"], metadata.Tags);
        Assert.Equal(["Daredevil", "Karen Page"], metadata.Characters);
        Assert.Equal(["The Defenders"], metadata.Teams);
        Assert.Equal(["Hell's Kitchen"], metadata.Locations);
    }

    [Fact]
    public async Task Expands_metroninfo_credits_into_one_entry_per_role()
    {
        var path = CreateComic(metronInfo: MetronInfo);

        await using var comic = await ComicBook.OpenAsync(path);

        Assert.Equal(
            [
                new ComicCredit("Kevin Smith", ComicCreditRole.Writer),
                new ComicCredit("Joe Quesada", ComicCreditRole.Penciller),
                new ComicCredit("Joe Quesada", ComicCreditRole.CoverArtist),
                new ComicCredit("Jimmy Palmiotti", ComicCreditRole.Inker),
                new ComicCredit("Jimmy Palmiotti", ComicCreditRole.Custom("Ink Assists")),
            ],
            comic.Metadata.Credits);
    }

    [Fact]
    public async Task Orders_metroninfo_identifiers_with_the_primary_first()
    {
        var path = CreateComic(metronInfo: MetronInfo);

        await using var comic = await ComicBook.OpenAsync(path);

        Assert.Equal(
            [
                new ComicIdentifier("metron", "678"),
                new ComicIdentifier("comicvine", "12345"),
                new ComicIdentifier("grandcomicsdatabase", "91011"),
                new ComicIdentifier("isbn", "9780785134794"),
                new ComicIdentifier("upc", "759606044313"),
            ],
            comic.Metadata.Identifiers);
    }

    [Fact]
    public async Task Orders_metroninfo_urls_with_the_primary_first()
    {
        var path = CreateComic(metronInfo: MetronInfo);

        await using var comic = await ComicBook.OpenAsync(path);

        Assert.Equal(
            [
                new Uri("https://metron.cloud/issue/daredevil-1998-1/"),
                new Uri("https://comicvine.gamespot.com/daredevil-1/4000-12345/"),
            ],
            comic.Metadata.WebLinks);
    }

    [Fact]
    public async Task Uses_a_single_story_as_the_title_and_retains_the_rest_in_extensions()
    {
        var path = CreateComic(metronInfo: MetronInfo);

        await using var comic = await ComicBook.OpenAsync(path);
        var extensions = comic.Metadata.Extensions;

        Assert.Equal("Guardian Devil, Part One", comic.Metadata.Title);
        Assert.Equal("Guardian Devil, Part One", extensions["Stories"]);
        Assert.Equal("Guardian Devil #1", extensions["Arcs"]);
        Assert.Equal("2.99 US", extensions["Prices"]);
        Assert.Equal("1998-09-16", extensions["StoreDate"]);
        Assert.Equal("1998", extensions["Series.StartYear"]);
        Assert.Equal("Dan Defensor", extensions["Series.AlternativeNames"]);
    }

    [Fact]
    public async Task Prefers_a_collection_title_and_leaves_title_unset_for_several_stories()
    {
        const string collection = """
            <MetronInfo>
              <Series><Name>Daredevil</Name></Series>
              <CollectionTitle>Guardian Devil</CollectionTitle>
              <Stories><Story>Part One</Story><Story>Part Two</Story></Stories>
            </MetronInfo>
            """;
        const string anthology = """
            <MetronInfo>
              <Series><Name>Daredevil</Name></Series>
              <Stories><Story>Part One</Story><Story>Part Two</Story></Stories>
            </MetronInfo>
            """;

        await using var collected = await ComicBook.OpenAsync(CreateComic(metronInfo: collection));
        await using var anthologised = await ComicBook.OpenAsync(CreateComic(metronInfo: anthology, fileName: "anthology.cbz"));

        Assert.Equal("Guardian Devil", collected.Metadata.Title);
        Assert.Null(anthologised.Metadata.Title);
        Assert.Equal("Part One, Part Two", anthologised.Metadata.Extensions["Stories"]);
    }

    [Fact]
    public async Task Treats_metroninfo_schema_defaults_as_undeclared()
    {
        // PageCount defaults to 0 and AgeRating to Unknown; neither is a declared value.
        const string defaults = """
            <MetronInfo>
              <Series><Name>Daredevil</Name></Series>
              <PageCount>0</PageCount>
              <AgeRating>Unknown</AgeRating>
            </MetronInfo>
            """;

        await using var comic = await ComicBook.OpenAsync(CreateComic(metronInfo: defaults));

        Assert.Null(comic.Metadata.DeclaredPageCount);
        Assert.Null(comic.Metadata.AgeRating);
    }

    [Fact]
    public async Task Ranks_metroninfo_below_comicinfo_and_above_comet()
    {
        var path = CreateComic(
            comicInfo: "<ComicInfo><Series>From ComicInfo</Series></ComicInfo>",
            metronInfo: MetronInfo,
            coMet: CoMet);

        await using var comic = await ComicBook.OpenAsync(path);

        Assert.Equal("From ComicInfo", comic.Metadata.Series);
        Assert.Equal(
            [ComicMetadataStandard.ComicInfo, ComicMetadataStandard.MetronInfo, ComicMetadataStandard.CoMet],
            comic.Metadata.Sources.Select(source => source.Standard));

        await using var withoutComicInfo = await ComicBook.OpenAsync(
            CreateComic(metronInfo: MetronInfo, coMet: CoMet, fileName: "without-comicinfo.cbz"));

        Assert.Equal(ComicMetadataStandard.MetronInfo, withoutComicInfo.Metadata.Standard);
        Assert.Equal("Marvel Knights", withoutComicInfo.Metadata.Imprint);
    }

    [Fact]
    public async Task Falls_back_from_a_broken_comicinfo_to_metroninfo()
    {
        var path = CreateComic(comicInfo: "<ComicInfo><Series>truncated", metronInfo: MetronInfo);

        await using var comic = await ComicBook.OpenAsync(path);

        Assert.Equal(ComicMetadataStandard.MetronInfo, comic.Metadata.Standard);
        Assert.Equal("Daredevil", comic.Metadata.Series);
    }

    [Fact]
    public async Task Validation_recognises_metroninfo_and_reports_its_unknown_fields()
    {
        const string withUnknown = """
            <MetronInfo>
              <Series><Name>Daredevil</Name></Series>
              <SomethingNew>42</SomethingNew>
            </MetronInfo>
            """;

        var result = await ComicValidator.ValidateAsync(CreateComic(metronInfo: withUnknown));

        Assert.True(result.IsValid);
        Assert.DoesNotContain(result.Issues, issue => issue.Code is ComicValidationCode.UnsupportedEntryFormat);
        var warning = Assert.Single(result.Warnings, issue => issue.Code is ComicValidationCode.UnknownMetadataField);
        Assert.Contains("SomethingNew", warning.Message);
    }

    [Fact]
    public async Task Detection_reports_metroninfo_when_it_is_the_only_source()
    {
        var result = await ComicDetector.DetectAsync(CreateComic(metronInfo: MetronInfo));

        Assert.True(result.HasMetadata);
        Assert.Equal(ComicMetadataStandard.MetronInfo, result.MetadataStandard);
    }

    [Fact]
    public async Task Conversion_carries_metroninfo_across_verbatim()
    {
        var source = CreateComic(metronInfo: MetronInfo);
        var destination = _fixture.PathFor("converted-metron.cbz");

        await using (var comic = await ComicBook.OpenAsync(source))
        {
            await comic.ExportAsync(destination);
        }

        await using var converted = await ComicBook.OpenAsync(destination);

        Assert.Equal(ComicMetadataStandard.MetronInfo, converted.Metadata.Standard);
        Assert.Equal("Marvel Knights", converted.Metadata.Imprint);
        Assert.Equal("MetronInfo.xml", Assert.Single(converted.Resources).FileName);
    }

    [Fact]
    public async Task Reads_comet_into_the_same_common_model()
    {
        var path = CreateComic(coMet: CoMet);

        await using var comic = await ComicBook.OpenAsync(path);
        var metadata = comic.Metadata;

        Assert.Equal(ComicMetadataStandard.CoMet, metadata.Standard);
        Assert.Equal("Guardian Devil", metadata.Title);
        Assert.Equal("Daredevil", metadata.Series);
        Assert.Equal("1", metadata.Number);
        Assert.Equal("2", metadata.Volume);
        Assert.Equal("Marvel", metadata.Publisher);
        Assert.Equal(new DateOnly(1998, 11, 4), metadata.PublicationDate);
        Assert.Equal("A synopsis.", metadata.Summary);
        Assert.Equal("Teen", metadata.AgeRating);
        Assert.Equal(3, metadata.DeclaredPageCount);
    }

    [Fact]
    public async Task Reads_repeated_comet_elements_as_lists()
    {
        var path = CreateComic(coMet: CoMet);

        await using var comic = await ComicBook.OpenAsync(path);

        Assert.Equal(["Superhero", "Crime"], comic.Metadata.Genres);
        Assert.Equal(["Daredevil", "Karen Page"], comic.Metadata.Characters);
        Assert.Equal(
            [
                new ComicCredit("Kevin Smith", ComicCreditRole.Writer),
                new ComicCredit("Joe Quesada", ComicCreditRole.Penciller),
                new ComicCredit("Joe Quesada", ComicCreditRole.CoverArtist),
            ],
            comic.Metadata.Credits);
    }

    [Fact]
    public async Task Maps_comet_right_to_left_reading_and_preserves_the_rest()
    {
        var path = CreateComic(coMet: CoMet);

        await using var comic = await ComicBook.OpenAsync(path);

        Assert.Equal(MangaReadingDirection.YesRightToLeft, comic.Metadata.Manga);
        Assert.Equal("Copyright Marvel", comic.Metadata.Extensions["rights"]);
        Assert.Equal(
            new ComicIdentifier("comet", "urn:isbn:9780785134794"),
            Assert.Single(comic.Metadata.Identifiers));
    }

    [Fact]
    public async Task Reads_comicbookinfo_from_the_archive_comment()
    {
        var path = CreateComic(comment: ComicBookInfo);

        await using var comic = await ComicBook.OpenAsync(path);
        var metadata = comic.Metadata;

        Assert.Equal(ComicMetadataStandard.ComicBookInfo, metadata.Standard);
        Assert.Equal("(archive comment)", metadata.PrimarySource!.Location);
        Assert.Equal("Daredevil", metadata.Series);
        Assert.Equal("1", metadata.Number);
        Assert.Equal(8, metadata.Count);
        Assert.Equal(1998, metadata.Year);
        Assert.Equal(11, metadata.Month);
        Assert.Equal("US", metadata.Country);
        Assert.Equal(4.5, metadata.CommunityRating);
        Assert.Equal("A synopsis.", metadata.Summary);
        Assert.Equal(["classic", "noir"], metadata.Tags);
        Assert.Equal(["Superhero", "Crime"], metadata.Genres);
    }

    [Fact]
    public async Task Normalises_comicbookinfo_credit_roles()
    {
        var path = CreateComic(comment: ComicBookInfo);

        await using var comic = await ComicBook.OpenAsync(path);

        Assert.Equal(
            [
                new ComicCredit("Kevin Smith", ComicCreditRole.Writer),
                new ComicCredit("Joe Quesada", ComicCreditRole.Penciller),
            ],
            comic.Metadata.Credits);
    }

    [Fact]
    public async Task Prefers_comicinfo_and_still_reports_the_other_sources()
    {
        var path = CreateComic(
            comicInfo: "<ComicInfo><Series>From ComicInfo</Series></ComicInfo>",
            coMet: CoMet,
            comment: ComicBookInfo);

        await using var comic = await ComicBook.OpenAsync(path);

        Assert.Equal("From ComicInfo", comic.Metadata.Series);
        Assert.Equal(
            [ComicMetadataStandard.ComicInfo, ComicMetadataStandard.CoMet, ComicMetadataStandard.ComicBookInfo],
            comic.Metadata.Sources.Select(source => source.Standard));
        Assert.Equal(ComicMetadataStandard.ComicInfo, comic.Metadata.PrimarySource!.Standard);
        Assert.Single(comic.Metadata.Sources, source => source.IsPrimary);
    }

    [Fact]
    public async Task Falls_back_to_the_next_source_when_the_preferred_one_is_broken()
    {
        var path = CreateComic(comicInfo: "<ComicInfo><Series>truncated", coMet: CoMet);

        await using var comic = await ComicBook.OpenAsync(path);

        Assert.Equal(ComicMetadataStandard.CoMet, comic.Metadata.Standard);
        Assert.Equal("Daredevil", comic.Metadata.Series);
    }

    [Fact]
    public async Task Reports_the_broken_source_as_a_warning_without_invalidating_the_comic()
    {
        var path = CreateComic(comicInfo: "<ComicInfo><Series>truncated", coMet: CoMet);

        var result = await ComicValidator.ValidateAsync(path);

        Assert.True(result.IsValid);
        var warning = Assert.Single(
            result.Warnings,
            issue => issue.Code is ComicValidationCode.MetadataUnreadable);
        Assert.Equal("ComicInfo.xml", warning.EntryName);
    }

    [Fact]
    public async Task Does_not_treat_comet_or_comicinfo_as_unsupported_files()
    {
        var path = CreateComic(comicInfo: "<ComicInfo><Series>Batman</Series></ComicInfo>", coMet: CoMet);

        var result = await ComicValidator.ValidateAsync(path);

        Assert.DoesNotContain(result.Issues, issue => issue.Code is ComicValidationCode.UnsupportedEntryFormat);
    }

    [Fact]
    public async Task Detection_reports_the_standard_that_would_win()
    {
        var path = CreateComic(coMet: CoMet, comment: ComicBookInfo);

        var result = await ComicDetector.DetectAsync(path);

        Assert.True(result.HasMetadata);
        Assert.Equal(ComicMetadataStandard.CoMet, result.MetadataStandard);
    }

    [Fact]
    public async Task Conversion_carries_the_archive_comment_so_comicbookinfo_survives()
    {
        var source = CreateComic(comment: ComicBookInfo);
        var destination = _fixture.PathFor("converted.cbz");

        await using (var comic = await ComicBook.OpenAsync(source))
        {
            await comic.ExportAsync(destination);
        }

        await using var converted = await ComicBook.OpenAsync(destination);

        Assert.Equal(ComicMetadataStandard.ComicBookInfo, converted.Metadata.Standard);
        Assert.Equal("Daredevil", converted.Metadata.Series);
    }

    private string CreateComic(
        string? comicInfo = null,
        string? coMet = null,
        string? comment = null,
        string? metronInfo = null,
        string fileName = "comic.cbz")
    {
        var entries = new List<(string, byte[])>
        {
            ("1.jpg", TestImages.Jpeg(1)),
            ("2.jpg", TestImages.Jpeg(2)),
            ("3.jpg", TestImages.Jpeg(3)),
        };

        if (comicInfo is not null)
        {
            entries.Add(("ComicInfo.xml", Encoding.UTF8.GetBytes(comicInfo)));
        }

        if (metronInfo is not null)
        {
            entries.Add(("MetronInfo.xml", Encoding.UTF8.GetBytes(metronInfo)));
        }

        if (coMet is not null)
        {
            entries.Add(("comet.xml", Encoding.UTF8.GetBytes(coMet)));
        }

        return _fixture.CreateZip(fileName, entries, comment);
    }

    public void Dispose() => _fixture.Dispose();
}
