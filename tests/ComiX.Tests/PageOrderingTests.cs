using ComiX.Tests.Fixtures;
using Xunit;

namespace ComiX.Tests;

public sealed class PageOrderingTests : IDisposable
{
    private readonly ComicFixture _fixture = new();

    [Fact]
    public async Task Orders_numbers_numerically_not_alphabetically()
    {
        var path = _fixture.CreateZip("comic.cbz",
        [
            ("10.jpg", TestImages.Jpeg(10)),
            ("2.jpg", TestImages.Jpeg(2)),
            ("1.jpg", TestImages.Jpeg(1)),
        ]);

        var names = await PageNamesAsync(path);

        Assert.Equal(["1.jpg", "2.jpg", "10.jpg"], names);
    }

    [Fact]
    public async Task Ignores_zero_padding_when_ordering()
    {
        var path = _fixture.CreateZip("comic.cbz",
        [
            ("page-010.jpg", TestImages.Jpeg(10)),
            ("page-2.jpg", TestImages.Jpeg(2)),
            ("page-001.jpg", TestImages.Jpeg(1)),
        ]);

        var names = await PageNamesAsync(path);

        Assert.Equal(["page-001.jpg", "page-2.jpg", "page-010.jpg"], names);
    }

    [Fact]
    public async Task Puts_a_root_cover_first()
    {
        var path = _fixture.CreateZip("comic.cbz",
        [
            ("001.jpg", TestImages.Jpeg(1)),
            ("002.jpg", TestImages.Jpeg(2)),
            ("cover.jpg", TestImages.Jpeg(0)),
        ]);

        var names = await PageNamesAsync(path);

        Assert.Equal(["cover.jpg", "001.jpg", "002.jpg"], names);
    }

    [Fact]
    public async Task Keeps_suffixed_pages_next_to_the_page_they_extend()
    {
        var path = _fixture.CreateZip("comic.cbz",
        [
            ("001b.jpg", TestImages.Jpeg(2)),
            ("002.jpg", TestImages.Jpeg(3)),
            ("001a.jpg", TestImages.Jpeg(1)),
        ]);

        var names = await PageNamesAsync(path);

        Assert.Equal(["001a.jpg", "001b.jpg", "002.jpg"], names);
    }

    [Fact]
    public async Task Orders_by_folder_before_file_name()
    {
        var path = _fixture.CreateZip("comic.cbz",
        [
            ("chapter-10/1.jpg", TestImages.Jpeg(3)),
            ("chapter-2/2.jpg", TestImages.Jpeg(2)),
            ("chapter-2/1.jpg", TestImages.Jpeg(1)),
        ]);

        var names = await PageNamesAsync(path);

        Assert.Equal(["chapter-2/1.jpg", "chapter-2/2.jpg", "chapter-10/1.jpg"], names);
    }

    [Fact]
    public async Task Excludes_non_images_and_archiver_junk_from_pages()
    {
        var path = _fixture.CreateZip("comic.cbz",
        [
            ("1.jpg", TestImages.Jpeg(1)),
            ("readme.txt", TestImages.NotAnImage()),
            ("__MACOSX/._1.jpg", TestImages.NotAnImage()),
            ("Thumbs.db", TestImages.NotAnImage()),
        ]);

        await using var comic = await ComicBook.OpenAsync(path);

        Assert.Equal(["1.jpg"], comic.Pages.Select(page => page.Name));
        Assert.Equal(["readme.txt"], comic.Resources.Select(resource => resource.Name));
    }

    [Fact]
    public async Task Recognises_every_supported_image_extension()
    {
        var path = _fixture.CreateZip("comic.cbz",
        [
            ("1.jpeg", TestImages.Jpeg(1)),
            ("2.png", TestImages.Png(2)),
            ("3.webp", TestImages.Jpeg(3)),
            ("4.gif", TestImages.Jpeg(4)),
            ("5.bmp", TestImages.Jpeg(5)),
            ("6.tiff", TestImages.Jpeg(6)),
            ("7.avif", TestImages.Jpeg(7)),
        ]);

        await using var comic = await ComicBook.OpenAsync(path);

        Assert.Equal(7, comic.Pages.Count);
        Assert.Equal(ComicImageFormat.Png, comic.Pages[1].ImageFormat);
        Assert.Equal("image/webp", comic.Pages[2].MimeType);
    }

    private static async Task<string[]> PageNamesAsync(string path)
    {
        await using var comic = await ComicBook.OpenAsync(path);
        return [.. comic.Pages.Select(page => page.Name)];
    }

    public void Dispose() => _fixture.Dispose();
}
