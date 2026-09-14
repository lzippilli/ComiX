using ComiX.Tests.Fixtures;
using Xunit;

namespace ComiX.Tests;

public sealed class ExtractionTests : IDisposable
{
    private readonly ComicFixture _fixture = new();

    [Fact]
    public async Task Extracts_a_single_page_to_a_file()
    {
        var path = _fixture.CreateStandardComic();
        var destination = Path.Combine(_fixture.Root, "out", "page-001.jpg");

        await using var comic = await ComicBook.OpenAsync(path);
        await comic.Pages[0].ExtractAsync(destination);

        Assert.Equal(TestImages.Jpeg(1), await File.ReadAllBytesAsync(destination));
    }

    [Fact]
    public async Task Extracts_a_single_page_to_a_stream_without_disposing_it()
    {
        var path = _fixture.CreateStandardComic();
        using var destination = new MemoryStream();

        await using var comic = await ComicBook.OpenAsync(path);
        await comic.Pages[1].ExtractAsync(destination);

        Assert.Equal(TestImages.Jpeg(2), destination.ToArray());
        destination.WriteByte(0);
    }

    [Fact]
    public async Task Refuses_to_overwrite_unless_asked()
    {
        var path = _fixture.CreateStandardComic();
        var destination = Path.Combine(_fixture.Root, "page.jpg");
        await File.WriteAllTextAsync(destination, "existing");

        await using var comic = await ComicBook.OpenAsync(path);

        await Assert.ThrowsAsync<IOException>(() => comic.Pages[0].ExtractAsync(destination));

        await comic.Pages[0].ExtractAsync(destination, overwrite: true);
        Assert.Equal(TestImages.Jpeg(1), await File.ReadAllBytesAsync(destination));
    }

    [Fact]
    public async Task Extracts_everything_preserving_the_archive_structure()
    {
        var path = _fixture.CreateZip("comic.cbz",
        [
            ("chapter-1/1.jpg", TestImages.Jpeg(1)),
            ("readme.txt", TestImages.NotAnImage()),
        ]);
        var destination = Path.Combine(_fixture.Root, "out");

        await using var comic = await ComicBook.OpenAsync(path);
        await comic.ExtractAllAsync(destination);

        Assert.True(File.Exists(Path.Combine(destination, "chapter-1", "1.jpg")));
        Assert.True(File.Exists(Path.Combine(destination, "readme.txt")));
    }

    [Fact]
    public async Task Can_skip_resources_and_flatten_the_structure()
    {
        var path = _fixture.CreateZip("comic.cbz",
        [
            ("chapter-1/1.jpg", TestImages.Jpeg(1)),
            ("readme.txt", TestImages.NotAnImage()),
        ]);
        var destination = Path.Combine(_fixture.Root, "flat");

        await using var comic = await ComicBook.OpenAsync(path);
        await comic.ExtractAllAsync(
            destination,
            new ComicExtractionOptions { Flatten = true, IncludeResources = false });

        Assert.True(File.Exists(Path.Combine(destination, "1.jpg")));
        Assert.False(File.Exists(Path.Combine(destination, "readme.txt")));
    }

    [Fact]
    public async Task Cannot_be_tricked_into_writing_outside_the_destination()
    {
        var path = _fixture.CreateZip("evil.cbz",
        [
            ("../escaped.jpg", TestImages.Jpeg(1)),
            ("../../deeper/escaped.jpg", TestImages.Jpeg(2)),
        ]);
        var destination = Path.Combine(_fixture.Root, "out");

        await using var comic = await ComicBook.OpenAsync(path);
        await comic.ExtractAllAsync(destination);

        Assert.True(File.Exists(Path.Combine(destination, "escaped.jpg")));
        Assert.True(File.Exists(Path.Combine(destination, "deeper", "escaped.jpg")));
        Assert.False(File.Exists(Path.Combine(_fixture.Root, "escaped.jpg")));
    }

    [Fact]
    public async Task Stops_when_cancelled()
    {
        var entries = Enumerable.Range(0, 40)
            .Select(i => ($"{i:D3}.jpg", TestImages.Jpeg((byte)i)))
            .ToList();
        var path = _fixture.CreateZip("comic.cbz", entries);
        var destination = Path.Combine(_fixture.Root, "out");

        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await using var comic = await ComicBook.OpenAsync(path);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => comic.ExtractAllAsync(destination, cancellationToken: cancellation.Token));
    }

    public void Dispose() => _fixture.Dispose();
}
