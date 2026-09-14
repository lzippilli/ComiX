using ComiX.Tests.Fixtures;
using Xunit;

namespace ComiX.Tests;

public sealed class MetadataTests : IDisposable
{
    private const string FullComicInfo = """
        <?xml version="1.0" encoding="utf-8"?>
        <ComicInfo xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
          <Title>The Killing Joke</Title>
          <Series>Batman</Series>
          <Number>1</Number>
          <Count>3</Count>
          <Volume>1988</Volume>
          <Summary>A one-shot.</Summary>
          <Year>1988</Year>
          <Month>3</Month>
          <Day>29</Day>
          <Writer>Alan Moore</Writer>
          <Penciller>Brian Bolland, John Higgins</Penciller>
          <Publisher>DC Comics</Publisher>
          <Genre>Superhero, Crime</Genre>
          <Web>https://example.org/killing-joke</Web>
          <PageCount>3</PageCount>
          <LanguageISO>en</LanguageISO>
          <BlackAndWhite>No</BlackAndWhite>
          <Manga>YesAndRightToLeft</Manga>
          <AgeRating>Mature 17+</AgeRating>
          <Characters>Batman, Joker</Characters>
          <StoryArc>One Shots</StoryArc>
          <GTIN>9781401216672</GTIN>
          <Pages>
            <Page Image="0" Type="FrontCover" ImageWidth="600" ImageHeight="900" />
            <Page Image="1" Type="Story" DoublePage="true" Bookmark="Chapter 1" />
          </Pages>
        </ComicInfo>
        """;

    private readonly ComicFixture _fixture = new();

    [Fact]
    public async Task Reads_a_complete_comicinfo_into_the_common_model()
    {
        var path = _fixture.CreateStandardComic("comic.cbz", FullComicInfo);

        await using var comic = await ComicBook.OpenAsync(path);
        var metadata = comic.Metadata;

        Assert.Equal(ComicMetadataStandard.ComicInfo, metadata.Standard);
        Assert.Equal("The Killing Joke", metadata.Title);
        Assert.Equal("Batman", metadata.Series);
        Assert.Equal("1", metadata.Number);
        Assert.Equal(3, metadata.Count);
        Assert.Equal("1988", metadata.Volume);
        Assert.Equal("DC Comics", metadata.Publisher);
        Assert.Equal(new DateOnly(1988, 3, 29), metadata.PublicationDate);
        Assert.Equal(["Superhero", "Crime"], metadata.Genres);
        Assert.Equal(["Batman", "Joker"], metadata.Characters);
        Assert.Equal("en", metadata.Language);
        Assert.False(metadata.BlackAndWhite);
        Assert.Equal(MangaReadingDirection.YesRightToLeft, metadata.Manga);
        Assert.Equal("Mature 17+", metadata.AgeRating);
        Assert.Equal(3, metadata.DeclaredPageCount);
        Assert.Equal(new Uri("https://example.org/killing-joke"), Assert.Single(metadata.WebLinks));
        Assert.Equal(new ComicIdentifier("gtin", "9781401216672"), Assert.Single(metadata.Identifiers));
    }

    [Fact]
    public async Task Models_multiple_people_in_one_role_as_separate_credits()
    {
        var path = _fixture.CreateStandardComic("comic.cbz", FullComicInfo);

        await using var comic = await ComicBook.OpenAsync(path);

        Assert.Equal(
            [
                new ComicCredit("Alan Moore", ComicCreditRole.Writer),
                new ComicCredit("Brian Bolland", ComicCreditRole.Penciller),
                new ComicCredit("John Higgins", ComicCreditRole.Penciller),
            ],
            comic.Metadata.Credits);
    }

    [Fact]
    public async Task Credits_one_person_once_per_role_they_are_credited_for()
    {
        // ComicInfo credits the same person twice; each role is its own credit, and callers who
        // want one entry per person group by name.
        const string comicInfo = """
            <ComicInfo>
              <Writer>Frank Miller</Writer>
              <Penciller>Frank Miller, Klaus Janson</Penciller>
            </ComicInfo>
            """;
        var path = _fixture.CreateStandardComic("comic.cbz", comicInfo);

        await using var comic = await ComicBook.OpenAsync(path);

        Assert.Equal(
            [ComicCreditRole.Writer, ComicCreditRole.Penciller],
            comic.Metadata.Credits
                .Where(credit => credit.Name == "Frank Miller")
                .Select(credit => credit.Role));
    }

    [Fact]
    public async Task Lists_every_metadata_source_and_marks_the_one_that_won()
    {
        var path = _fixture.CreateStandardComic("comic.cbz", FullComicInfo);

        await using var comic = await ComicBook.OpenAsync(path);

        var source = Assert.Single(comic.Metadata.Sources);
        Assert.Equal(ComicMetadataStandard.ComicInfo, source.Standard);
        Assert.Equal("ComicInfo.xml", source.Location);
        Assert.True(source.IsPrimary);
        Assert.Same(source, comic.Metadata.PrimarySource);
    }

    [Fact]
    public async Task Reports_no_sources_when_the_archive_carries_no_metadata()
    {
        var path = _fixture.CreateStandardComic();

        await using var comic = await ComicBook.OpenAsync(path);

        Assert.Empty(comic.Metadata.Sources);
        Assert.Null(comic.Metadata.PrimarySource);
        Assert.Equal(ComicMetadataStandard.None, comic.Metadata.Standard);
    }

    [Fact]
    public async Task Applies_page_metadata_to_the_matching_pages()
    {
        var path = _fixture.CreateStandardComic("comic.cbz", FullComicInfo);

        await using var comic = await ComicBook.OpenAsync(path);

        Assert.Equal(ComicPageType.FrontCover, comic.Pages[0].Type);
        Assert.Equal(600, comic.Pages[0].DeclaredWidth);
        Assert.Equal(900, comic.Pages[0].DeclaredHeight);
        Assert.Equal(ComicPageSpread.Double, comic.Pages[1].DeclaredSpread);
        Assert.Equal("Chapter 1", comic.Pages[1].Bookmark);
        Assert.Null(comic.Pages[2].DeclaredWidth);
        Assert.Equal(ComicPageType.Unknown, comic.Pages[2].Type);
    }

    [Fact]
    public async Task Preserves_standard_specific_and_unknown_fields_instead_of_dropping_them()
    {
        const string comicInfo = """
            <ComicInfo>
              <Series>Batman</Series>
              <StoryArc>Knightfall</StoryArc>
              <SomethingNobodyStandardised>42</SomethingNobodyStandardised>
            </ComicInfo>
            """;
        var path = _fixture.CreateStandardComic("comic.cbz", comicInfo);

        await using var comic = await ComicBook.OpenAsync(path);

        Assert.Equal("Knightfall", comic.Metadata.Extensions["StoryArc"]);
        Assert.Equal("42", comic.Metadata.Extensions["SomethingNobodyStandardised"]);
    }

    [Fact]
    public async Task Leaves_unset_fields_null_for_a_partial_comicinfo()
    {
        var path = _fixture.CreateStandardComic("comic.cbz", "<ComicInfo><Series>Batman</Series></ComicInfo>");

        await using var comic = await ComicBook.OpenAsync(path);

        Assert.Equal("Batman", comic.Metadata.Series);
        Assert.Null(comic.Metadata.Title);
        Assert.Null(comic.Metadata.PublicationDate);
        Assert.Empty(comic.Metadata.Credits);
    }

    [Fact]
    public async Task Ignores_values_that_do_not_parse_instead_of_failing()
    {
        const string comicInfo = """
            <ComicInfo>
              <Series>Batman</Series>
              <Year>nineteen eighty eight</Year>
              <Count>lots</Count>
              <Web>not a url</Web>
            </ComicInfo>
            """;
        var path = _fixture.CreateStandardComic("comic.cbz", comicInfo);

        await using var comic = await ComicBook.OpenAsync(path);

        Assert.Equal("Batman", comic.Metadata.Series);
        Assert.Null(comic.Metadata.Year);
        Assert.Null(comic.Metadata.Count);
        Assert.Empty(comic.Metadata.WebLinks);
        Assert.Equal("not a url", comic.Metadata.Extensions["Web"]);
    }

    [Fact]
    public async Task Broken_metadata_does_not_make_a_valid_comic_unreadable()
    {
        var path = _fixture.CreateStandardComic("comic.cbz", "<ComicInfo><Series>Batman</Ser");

        await using var comic = await ComicBook.OpenAsync(path);

        Assert.Equal(3, comic.Pages.Count);
        Assert.Null(comic.Metadata.Series);
        Assert.Equal(ComicMetadataStandard.None, comic.Metadata.Standard);

        // The unreadable file is still reported as present, so the archive does not look untagged.
        var source = Assert.Single(comic.Metadata.Sources);
        Assert.Equal(ComicMetadataStandard.ComicInfo, source.Standard);
        Assert.False(source.IsPrimary);
    }

    [Fact]
    public async Task Does_not_resolve_external_entities()
    {
        // A classic XXE payload: parsing must fail rather than read the referenced file.
        var secret = _fixture.CreateRaw("secret.txt", "top secret"u8.ToArray());
        var comicInfo = $"""
            <?xml version="1.0"?>
            <!DOCTYPE ComicInfo [<!ENTITY xxe SYSTEM "file:///{secret.Replace('\\', '/')}">]>
            <ComicInfo><Series>&xxe;</Series></ComicInfo>
            """;
        var path = _fixture.CreateStandardComic("comic.cbz", comicInfo);

        await using var comic = await ComicBook.OpenAsync(path);

        Assert.Null(comic.Metadata.Series);
        Assert.Equal(ComicMetadataStandard.None, comic.Metadata.Standard);
    }

    [Fact]
    public async Task Skips_metadata_entirely_when_asked_to()
    {
        var path = _fixture.CreateStandardComic("comic.cbz", FullComicInfo);

        await using var comic = await ComicBook.OpenAsync(path, new ComicOpenOptions { ReadMetadata = false });

        Assert.Same(ComicMetadata.Empty, comic.Metadata);
        Assert.Equal(3, comic.Pages.Count);
    }

    public void Dispose() => _fixture.Dispose();
}
