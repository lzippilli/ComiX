using ComiX.Tests.Fixtures;
using Xunit;

namespace ComiX.Tests;

public sealed class ComicBookTests : IDisposable
{
    private readonly ComicFixture _fixture = new();

    [Fact]
    public async Task Opens_an_archive_with_no_entries()
    {
        var path = _fixture.CreateZip("empty.cbz", []);

        await using var comic = await ComicBook.OpenAsync(path);

        Assert.Empty(comic.Pages);
        Assert.Empty(comic.Resources);
    }

    [Fact]
    public async Task Opens_an_archive_that_contains_no_images()
    {
        var path = _fixture.CreateZip("notes.cbz", [("readme.txt", TestImages.NotAnImage())]);

        await using var comic = await ComicBook.OpenAsync(path);

        Assert.Empty(comic.Pages);
        Assert.Single(comic.Resources);
    }

    [Fact]
    public async Task Reports_the_real_format_and_capabilities()
    {
        var path = _fixture.CreateStandardComic("mislabelled.cbr");

        await using var comic = await ComicBook.OpenAsync(path);

        Assert.Equal(ComicContainerFormat.Cbz, comic.Format);
        Assert.True(comic.Capabilities.HasFlag(ComicCapabilities.RandomAccess));
        Assert.True(comic.Capabilities.HasFlag(ComicCapabilities.Export));
    }

    [Fact]
    public async Task Reports_a_recognised_but_broken_container_as_an_archive_error()
    {
        // A 7-Zip signature with nothing behind it: the format is known, the content is not readable.
        var path = _fixture.CreateRaw("comic.cb7", [0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C, 0x00, 0x04]);

        await Assert.ThrowsAsync<ComicArchiveException>(() => ComicBook.OpenAsync(path));
    }

    [Fact]
    public async Task Rejects_a_file_that_is_not_an_archive()
    {
        var path = _fixture.CreateRaw("comic.cbz", "definitely not a zip"u8.ToArray());

        var exception = await Assert.ThrowsAsync<UnsupportedComicFormatException>(
            () => ComicBook.OpenAsync(path));

        Assert.Equal(ComicContainerFormat.Unknown, exception.Format);
    }

    [Fact]
    public async Task Refuses_archives_with_more_entries_than_allowed()
    {
        var entries = Enumerable.Range(0, 20)
            .Select(i => ($"{i}.jpg", TestImages.Jpeg((byte)i)))
            .ToList();
        var path = _fixture.CreateZip("many.cbz", entries);

        await Assert.ThrowsAsync<ComicArchiveException>(
            () => ComicBook.OpenAsync(path, new ComicOpenOptions { MaxEntryCount = 5 }));
    }

    [Fact]
    public async Task Refuses_to_read_an_entry_larger_than_allowed()
    {
        var path = _fixture.CreateZip("big.cbz", [("1.jpg", new byte[4096])]);

        await using var comic = await ComicBook.OpenAsync(
            path,
            new ComicOpenOptions { MaxEntrySizeInBytes = 128 });

        await Assert.ThrowsAsync<ComicArchiveException>(() => comic.Pages[0].OpenAsync());
    }

    [Fact]
    public async Task Applies_the_size_limit_to_streaming_extraction_too()
    {
        // The limit lives in the content reader, not in the archive backend, so it applies to every
        // way of reading an entry rather than only to the buffered one.
        var path = _fixture.CreateZip("big.cbz", [("1.jpg", new byte[4096])]);
        using var destination = new MemoryStream();

        await using var comic = await ComicBook.OpenAsync(
            path,
            new ComicOpenOptions { MaxEntrySizeInBytes = 128 });

        await Assert.ThrowsAsync<ComicArchiveException>(() => comic.Pages[0].ExtractAsync(destination));
    }

    [Fact]
    public async Task Opens_from_a_caller_owned_stream_and_leaves_it_open()
    {
        var path = _fixture.CreateStandardComic();
        await using var stream = File.OpenRead(path);

        await using (var comic = await ComicBook.OpenAsync(stream))
        {
            Assert.Equal(3, comic.Pages.Count);
            Assert.Null(comic.FilePath);
        }

        Assert.True(stream.CanRead);
    }

    [Fact]
    public async Task Rejects_a_stream_that_cannot_seek()
    {
        await using var stream = new NonSeekableStream();

        await Assert.ThrowsAsync<ArgumentException>(() => ComicBook.OpenAsync(stream));
    }

    [Fact]
    public async Task Page_streams_stay_valid_after_the_comic_is_disposed()
    {
        var path = _fixture.CreateStandardComic();

        Stream page;
        await using (var comic = await ComicBook.OpenAsync(path))
        {
            page = await comic.Pages[0].OpenAsync();
        }

        await using (page)
        {
            var buffer = new byte[16];
            var read = await page.ReadAsync(buffer);
            Assert.Equal(TestImages.Jpeg(1).Length, read);
        }
    }

    [Fact]
    public async Task Reads_pages_concurrently_without_mixing_their_content()
    {
        var path = _fixture.CreateZip("comic.cbz",
        [
            ("1.jpg", TestImages.Jpeg(1)),
            ("2.jpg", TestImages.Jpeg(2)),
            ("3.jpg", TestImages.Jpeg(3)),
        ]);

        await using var comic = await ComicBook.OpenAsync(path);

        var reads = comic.Pages.Select(async page =>
        {
            await using var stream = await page.OpenAsync();
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            return buffer.ToArray();
        });

        var contents = await Task.WhenAll(reads);

        Assert.Equal(TestImages.Jpeg(1), contents[0]);
        Assert.Equal(TestImages.Jpeg(2), contents[1]);
        Assert.Equal(TestImages.Jpeg(3), contents[2]);
    }

    public void Dispose() => _fixture.Dispose();

    private sealed class NonSeekableStream : MemoryStream
    {
        public override bool CanSeek => false;
    }
}
