using System.Globalization;
using ComiX.Tests.Fixtures;
using Xunit;

namespace ComiX.Tests;

/// <summary>
/// Reading entries from solid archives, where an entry cannot in general be decompressed without
/// decompressing the entries stored before it.
/// </summary>
/// <remarks>
/// The fixture <c>solid.cbr</c> is committed because RAR archives cannot be written from .NET. It
/// holds ComicInfo.xml and three pages whose stored order (1, 10, 2) differs from their reading
/// order (1, 2, 10), so reaching a page requires skipping over one that is not wanted. Direct access
/// to any of its pages fails in the underlying archive implementation, which is what these tests
/// exercise.
/// </remarks>
public sealed class SolidArchiveTests : IDisposable
{
    private readonly ComicFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task Reports_a_solid_archive_as_not_supporting_random_access()
    {
        await using var comic = await ComicBook.OpenAsync(_fixture.ExtractAsset("solid.cbr"));

        Assert.Equal(ComicContainerFormat.Cbr, comic.Format);
        Assert.False(comic.Capabilities.HasFlag(ComicCapabilities.RandomAccess));
        Assert.True(comic.Capabilities.HasFlag(ComicCapabilities.ReadContent));
    }

    [Fact]
    public async Task Reads_every_page_of_a_solid_archive()
    {
        await using var comic = await ComicBook.OpenAsync(_fixture.ExtractAsset("solid.cbr"));

        Assert.Equal(["1.jpg", "2.jpg", "10.jpg"], comic.Pages.Select(page => page.Name));

        foreach (var page in comic.Pages)
        {
            using var content = new MemoryStream();
            await page.ExtractAsync(content);

            Assert.Equal(TestImages.LargeJpeg(MarkerOf(page.Name)), content.ToArray());
        }
    }

    [Fact]
    public async Task Reads_pages_of_a_solid_archive_in_reverse_order()
    {
        // Reading backwards is the pattern least compatible with direct access: every page requested
        // is stored before the decoder's current position.
        await using var comic = await ComicBook.OpenAsync(_fixture.ExtractAsset("solid.cbr"));

        foreach (var page in comic.Pages.Reverse())
        {
            using var content = new MemoryStream();
            await page.ExtractAsync(content);

            Assert.Equal(TestImages.LargeJpeg(MarkerOf(page.Name)), content.ToArray());
        }
    }

    [Fact]
    public async Task Reads_the_same_page_of_a_solid_archive_twice()
    {
        await using var comic = await ComicBook.OpenAsync(_fixture.ExtractAsset("solid.cbr"));
        var page = comic.Pages[1];

        using var first = new MemoryStream();
        await page.ExtractAsync(first);

        using var second = new MemoryStream();
        await page.ExtractAsync(second);

        Assert.Equal(TestImages.LargeJpeg(2), first.ToArray());
        Assert.Equal(first.ToArray(), second.ToArray());
    }

    [Fact]
    public async Task Reads_a_solid_archive_opened_from_a_stream()
    {
        // Without a file path the sequential pass has to reuse the caller's stream, which it rewinds.
        var bytes = await File.ReadAllBytesAsync(_fixture.ExtractAsset("solid.cbr"));
        using var stream = new MemoryStream(bytes);

        await using var comic = await ComicBook.OpenAsync(stream);

        foreach (var page in comic.Pages.Reverse())
        {
            using var content = new MemoryStream();
            await page.ExtractAsync(content);

            Assert.Equal(TestImages.LargeJpeg(MarkerOf(page.Name)), content.ToArray());
        }
    }

    [Fact]
    public async Task Resolves_metadata_from_a_solid_archive()
    {
        await using var comic = await ComicBook.OpenAsync(_fixture.ExtractAsset("solid.cbr"));

        Assert.Equal("Batman", comic.Metadata.Series);
        Assert.Equal("Alan Moore", Assert.Single(comic.Metadata.Credits).Name);
    }

    [Fact]
    public async Task Extracts_every_page_of_a_solid_archive_completely()
    {
        await using var comic = await ComicBook.OpenAsync(_fixture.ExtractAsset("solid.cbr"));
        var destination = _fixture.PathFor("solid-out");

        await comic.ExtractAllAsync(destination);

        foreach (var name in new[] { "1.jpg", "2.jpg", "10.jpg" })
        {
            Assert.Equal(
                TestImages.LargeJpeg(MarkerOf(name)),
                await File.ReadAllBytesAsync(Path.Combine(destination, name)));
        }
    }

    [Fact]
    public async Task Deep_validation_of_a_solid_archive_reports_no_issue()
    {
        var result = await ComicValidator.ValidateAsync(
            _fixture.ExtractAsset("solid.cbr"),
            new ComicValidationOptions { DeepScan = true });

        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public async Task Reads_pages_of_a_solid_archive_in_stored_order()
    {
        // Stored order is the one traversal that never has to start again.
        await using var comic = await ComicBook.OpenAsync(_fixture.ExtractAsset("solid.cbr"));

        foreach (var page in comic.Pages.OrderBy(page => StoredPositionOf(page.Name)))
        {
            using var content = new MemoryStream();
            await page.ExtractAsync(content);

            Assert.Equal(TestImages.LargeJpeg(MarkerOf(page.Name)), content.ToArray());
        }
    }

    [Fact]
    public async Task Reads_the_last_page_of_a_solid_archive_and_then_the_first()
    {
        // The last page exhausts the traversal and the first then requires a new one.
        await using var comic = await ComicBook.OpenAsync(_fixture.ExtractAsset("solid.cbr"));

        using var last = new MemoryStream();
        await comic.Pages[^1].ExtractAsync(last);

        using var first = new MemoryStream();
        await comic.Pages[0].ExtractAsync(first);

        Assert.Equal(TestImages.LargeJpeg(10), last.ToArray());
        Assert.Equal(TestImages.LargeJpeg(1), first.ToArray());
    }

    [Fact]
    public async Task Releases_the_archive_file_when_a_solid_comic_is_disposed()
    {
        // Reading a solid archive opens a second handle for the sequential traversal. Windows refuses
        // to delete a file while any handle on it is open, so deletion is the assertion that both
        // were released.
        var path = _fixture.ExtractAsset("solid.cbr");

        await using (var comic = await ComicBook.OpenAsync(path))
        {
            using var content = new MemoryStream();
            await comic.Pages[^1].ExtractAsync(content);
        }

        File.Delete(path);

        Assert.False(File.Exists(path));
    }

    private static int StoredPositionOf(string pageName) =>
        pageName switch
        {
            "1.jpg" => 0,
            "10.jpg" => 1,
            _ => 2,
        };

    private static byte MarkerOf(string pageName) => byte.Parse(Path.GetFileNameWithoutExtension(pageName), CultureInfo.InvariantCulture);
}
