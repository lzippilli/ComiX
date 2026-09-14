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

    private readonly ComicFixture _fixture = new();

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

    private string CreateComic(string? comicInfo = null, string? coMet = null, string? comment = null)
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

        if (coMet is not null)
        {
            entries.Add(("comet.xml", Encoding.UTF8.GetBytes(coMet)));
        }

        return _fixture.CreateZip("comic.cbz", entries, comment);
    }

    public void Dispose() => _fixture.Dispose();
}
