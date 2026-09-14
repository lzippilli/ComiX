using System.Text;
using ComiX.Tests.Fixtures;
using Xunit;

namespace ComiX.Tests;

/// <summary>
/// Per-page information: what the metadata declares, what it leaves unsaid, and how it is matched to
/// the pages it describes.
/// </summary>
public sealed class PageMetadataTests : IDisposable
{
    private readonly ComicFixture _fixture = new();

    [Fact]
    public async Task Distinguishes_declared_single_declared_double_and_unsaid()
    {
        const string comicInfo = """
            <ComicInfo>
              <Pages>
                <Page Image="0" DoublePage="false" />
                <Page Image="1" DoublePage="true" />
                <Page Image="2" />
              </Pages>
            </ComicInfo>
            """;
        var path = CreateComic(comicInfo);

        await using var comic = await ComicBook.OpenAsync(path);

        Assert.Equal(ComicPageSpread.Single, comic.Pages[0].DeclaredSpread);
        Assert.Equal(ComicPageSpread.Double, comic.Pages[1].DeclaredSpread);

        // The schema defaults DoublePage to false, but an absent attribute means nobody looked.
        Assert.Equal(ComicPageSpread.Unknown, comic.Pages[2].DeclaredSpread);
    }

    [Fact]
    public async Task Reports_unknown_when_the_standard_has_no_page_information()
    {
        // CoMet describes the comic but never its individual pages.
        const string coMet = """
            <comet xmlns="http://www.denvog.com/comet/">
              <series>Daredevil</series>
              <pages>3</pages>
            </comet>
            """;
        var path = _fixture.CreateZip("comic.cbz",
        [
            ("1.jpg", TestImages.Jpeg(1)),
            ("2.jpg", TestImages.Jpeg(2)),
            ("3.jpg", TestImages.Jpeg(3)),
            ("comet.xml", Encoding.UTF8.GetBytes(coMet)),
        ]);

        await using var comic = await ComicBook.OpenAsync(path);

        Assert.All(comic.Pages, page => Assert.Equal(ComicPageSpread.Unknown, page.DeclaredSpread));
    }

    [Fact]
    public async Task Reports_unknown_for_a_comic_with_no_metadata_at_all()
    {
        var path = _fixture.CreateStandardComic();

        await using var comic = await ComicBook.OpenAsync(path);

        Assert.All(comic.Pages, page => Assert.Equal(ComicPageSpread.Unknown, page.DeclaredSpread));
    }

    [Fact]
    public async Task Treats_the_minus_one_dimension_sentinel_as_unknown()
    {
        // -1 is ComicInfo's schema default for "not measured", not a width.
        const string comicInfo = """
            <ComicInfo>
              <Pages>
                <Page Image="0" ImageWidth="-1" ImageHeight="-1" />
                <Page Image="1" ImageWidth="1988" ImageHeight="1200" />
              </Pages>
            </ComicInfo>
            """;
        var path = CreateComic(comicInfo);

        await using var comic = await ComicBook.OpenAsync(path);

        Assert.Null(comic.Pages[0].DeclaredWidth);
        Assert.Null(comic.Pages[0].DeclaredHeight);
        Assert.Equal(1988, comic.Pages[1].DeclaredWidth);
        Assert.Equal(1200, comic.Pages[1].DeclaredHeight);
    }

    [Fact]
    public async Task Matches_pages_by_name_when_the_metadata_names_them()
    {
        // The Image indices are deliberately wrong; the keys are right, and names win.
        const string comicInfo = """
            <ComicInfo>
              <Pages>
                <Page Image="2" Key="1.jpg" Type="FrontCover" />
                <Page Image="0" Key="2.jpg" DoublePage="true" />
                <Page Image="1" Key="10.jpg" Bookmark="End" />
              </Pages>
            </ComicInfo>
            """;
        var path = CreateComic(comicInfo);

        await using var comic = await ComicBook.OpenAsync(path);

        Assert.Equal(ComicPageType.FrontCover, comic.Pages[0].Type);
        Assert.Equal(ComicPageSpread.Double, comic.Pages[1].DeclaredSpread);
        Assert.Equal("End", comic.Pages[2].Bookmark);
    }

    [Fact]
    public async Task Falls_back_to_position_when_the_keys_do_not_fit_the_archive()
    {
        // Keys from a different archive: they must not be trusted, and positions still apply.
        const string comicInfo = """
            <ComicInfo>
              <Pages>
                <Page Image="0" Key="page-a.jpg" Type="FrontCover" />
                <Page Image="1" Key="page-b.jpg" DoublePage="true" />
                <Page Image="2" Key="page-c.jpg" />
              </Pages>
            </ComicInfo>
            """;
        var path = CreateComic(comicInfo);

        await using var comic = await ComicBook.OpenAsync(path);

        Assert.Equal(ComicPageType.FrontCover, comic.Pages[0].Type);
        Assert.Equal(ComicPageSpread.Double, comic.Pages[1].DeclaredSpread);
    }

    [Fact]
    public async Task Warns_when_the_metadata_describes_a_different_number_of_pages()
    {
        const string comicInfo = """
            <ComicInfo>
              <Pages>
                <Page Image="0" />
                <Page Image="1" />
              </Pages>
            </ComicInfo>
            """;
        var path = CreateComic(comicInfo);

        var result = await ComicValidator.ValidateAsync(path);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, issue => issue.Code is ComicValidationCode.PageMetadataMismatch);
    }

    [Fact]
    public async Task Does_not_warn_when_the_page_information_lines_up()
    {
        const string comicInfo = """
            <ComicInfo>
              <Pages>
                <Page Image="0" />
                <Page Image="1" />
                <Page Image="2" />
              </Pages>
            </ComicInfo>
            """;
        var path = CreateComic(comicInfo);

        var result = await ComicValidator.ValidateAsync(path);

        Assert.DoesNotContain(result.Issues, issue => issue.Code is ComicValidationCode.PageMetadataMismatch);
    }

    [Fact]
    public async Task Spread_information_survives_a_conversion()
    {
        const string comicInfo = """
            <ComicInfo>
              <Pages>
                <Page Image="1" DoublePage="true" />
              </Pages>
            </ComicInfo>
            """;
        var source = CreateComic(comicInfo);
        var destination = _fixture.PathFor("converted.cbz");

        await using (var comic = await ComicBook.OpenAsync(source))
        {
            await comic.ExportAsync(destination);
        }

        await using var converted = await ComicBook.OpenAsync(destination);

        Assert.Equal(ComicPageSpread.Double, converted.Pages[1].DeclaredSpread);
    }

    private string CreateComic(string comicInfo) =>
        _fixture.CreateZip("comic.cbz",
        [
            ("1.jpg", TestImages.Jpeg(1)),
            ("2.jpg", TestImages.Jpeg(2)),
            ("10.jpg", TestImages.Jpeg(10)),
            ("ComicInfo.xml", Encoding.UTF8.GetBytes(comicInfo)),
        ]);

    public void Dispose() => _fixture.Dispose();
}
