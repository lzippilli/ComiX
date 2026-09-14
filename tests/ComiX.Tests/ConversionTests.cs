using ComiX.Tests.Fixtures;
using Xunit;

namespace ComiX.Tests;

public sealed class ConversionTests : IDisposable
{
    private readonly ComicFixture _fixture = new();

    [Fact]
    public async Task Exports_pages_in_order_with_their_metadata()
    {
        const string comicInfo = "<ComicInfo><Series>Batman</Series><Number>7</Number></ComicInfo>";
        var source = _fixture.CreateStandardComic("source.cbz", comicInfo);
        var destination = _fixture.PathFor("converted.cbz");

        await using (var comic = await ComicBook.OpenAsync(source))
        {
            await comic.ExportAsync(destination);
        }

        await using var exported = await ComicBook.OpenAsync(destination);

        Assert.Equal(["1.jpg", "2.jpg", "10.jpg"], exported.Pages.Select(page => page.Name));
        Assert.Equal("Batman", exported.Metadata.Series);
        Assert.Equal("7", exported.Metadata.Number);
    }

    [Fact]
    public async Task Exports_page_content_unchanged()
    {
        var source = _fixture.CreateStandardComic();
        var destination = _fixture.PathFor("converted.cbz");

        await using (var comic = await ComicBook.OpenAsync(source))
        {
            await comic.ExportAsync(destination);
        }

        await using var exported = await ComicBook.OpenAsync(destination);
        await using var page = await exported.Pages[0].OpenAsync();
        using var buffer = new MemoryStream();
        await page.CopyToAsync(buffer);

        Assert.Equal(TestImages.Jpeg(1), buffer.ToArray());
    }

    [Fact]
    public async Task Exports_a_comic_that_has_no_metadata()
    {
        var source = _fixture.CreateStandardComic();
        var destination = _fixture.PathFor("converted.cbz");

        await using (var comic = await ComicBook.OpenAsync(source))
        {
            await comic.ExportAsync(destination);
        }

        await using var exported = await ComicBook.OpenAsync(destination);

        Assert.Equal(3, exported.Pages.Count);
        Assert.Same(ComicMetadata.Empty, exported.Metadata);
    }

    [Fact]
    public async Task Can_leave_resources_behind_when_asked()
    {
        var source = _fixture.CreateStandardComic("source.cbz", "<ComicInfo><Series>Batman</Series></ComicInfo>");
        var destination = _fixture.PathFor("pages-only.cbz");

        await using (var comic = await ComicBook.OpenAsync(source))
        {
            await comic.ExportAsync(destination, options: new ComicExportOptions { IncludeResources = false });
        }

        await using var exported = await ComicBook.OpenAsync(destination);

        Assert.Equal(3, exported.Pages.Count);
        Assert.Empty(exported.Resources);
    }

    [Fact]
    public async Task Refuses_to_overwrite_the_destination_unless_asked()
    {
        var source = _fixture.CreateStandardComic();
        var destination = _fixture.PathFor("existing.cbz");
        await File.WriteAllTextAsync(destination, "existing");

        await using var comic = await ComicBook.OpenAsync(source);

        await Assert.ThrowsAsync<IOException>(() => comic.ExportAsync(destination));

        await comic.ExportAsync(destination, options: new ComicExportOptions { Overwrite = true });
        Assert.True(new FileInfo(destination).Length > "existing".Length);
    }

    [Fact]
    public async Task Refuses_to_export_over_the_source_file()
    {
        var source = _fixture.CreateStandardComic();

        await using var comic = await ComicBook.OpenAsync(source);

        await Assert.ThrowsAsync<ComiXException>(
            () => comic.ExportAsync(source, options: new ComicExportOptions { Overwrite = true }));
    }

    [Fact]
    public async Task Leaves_no_half_written_archive_when_cancelled()
    {
        var source = _fixture.CreateStandardComic();
        var destination = _fixture.PathFor("cancelled.cbz");

        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await using var comic = await ComicBook.OpenAsync(source);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => comic.ExportAsync(destination, cancellationToken: cancellation.Token));

        Assert.False(File.Exists(destination));
    }

    public void Dispose() => _fixture.Dispose();
}
