using ComiX.Cache;
using ComiX.Tests.Fixtures;
using Xunit;

namespace ComiX.Tests;

public sealed class CacheTests : IDisposable
{
    private readonly ComicFixture _fixture = new();

    [Fact]
    public async Task Returns_the_same_content_on_a_cache_hit()
    {
        var path = _fixture.CreateStandardComic();
        var options = new ComicOpenOptions { Cache = ComicCache.InMemory() };

        await using var comic = await ComicBook.OpenAsync(path, options);

        var first = await ReadAsync(comic.Pages[0]);
        var second = await ReadAsync(comic.Pages[0]);

        Assert.Equal(first, second);
        Assert.Equal(TestImages.Jpeg(1), second);
    }

    [Fact]
    public async Task Two_comics_sharing_a_cache_do_not_serve_each_other_the_wrong_page()
    {
        var first = _fixture.CreateZip("first.cbz", [("1.jpg", TestImages.Jpeg(1))]);
        var second = _fixture.CreateZip("second.cbz", [("1.jpg", TestImages.Jpeg(9))]);
        var cache = ComicCache.InMemory();

        await using var firstComic = await ComicBook.OpenAsync(first, new ComicOpenOptions { Cache = cache });
        await using var secondComic = await ComicBook.OpenAsync(second, new ComicOpenOptions { Cache = cache });

        Assert.Equal(TestImages.Jpeg(1), await ReadAsync(firstComic.Pages[0]));
        Assert.Equal(TestImages.Jpeg(9), await ReadAsync(secondComic.Pages[0]));
    }

    [Fact]
    public async Task Rewriting_the_file_invalidates_what_was_cached_for_it()
    {
        var path = _fixture.CreateZip("comic.cbz", [("1.jpg", TestImages.Jpeg(1))]);
        var cache = ComicCache.InMemory();

        await using (var comic = await ComicBook.OpenAsync(path, new ComicOpenOptions { Cache = cache }))
        {
            Assert.Equal(TestImages.Jpeg(1), await ReadAsync(comic.Pages[0]));
        }

        // Same path, different content: the cache key includes size and last write time.
        File.Delete(path);
        _fixture.CreateZip("comic.cbz", [("1.jpg", TestImages.Png(2)), ("2.jpg", TestImages.Png(3))]);

        await using (var comic = await ComicBook.OpenAsync(path, new ComicOpenOptions { Cache = cache }))
        {
            Assert.Equal(TestImages.Png(2), await ReadAsync(comic.Pages[0]));
        }
    }

    [Fact]
    public async Task Serves_pages_correctly_when_read_concurrently_through_a_cache()
    {
        var path = _fixture.CreateZip("comic.cbz",
        [
            ("1.jpg", TestImages.Jpeg(1)),
            ("2.jpg", TestImages.Jpeg(2)),
        ]);

        await using var comic = await ComicBook.OpenAsync(
            path,
            new ComicOpenOptions { Cache = ComicCache.InMemory() });

        var reads = Enumerable.Range(0, 20)
            .Select(i => ReadAsync(comic.Pages[i % 2]));

        var results = await Task.WhenAll(reads);

        Assert.All(results.Where((_, i) => i % 2 == 0), content => Assert.Equal(TestImages.Jpeg(1), content));
        Assert.All(results.Where((_, i) => i % 2 == 1), content => Assert.Equal(TestImages.Jpeg(2), content));
    }

    [Fact]
    public async Task Memory_cache_evicts_the_least_recently_used_entry_when_full()
    {
        var cache = new MemoryComicCache(maxSizeInBytes: 20);

        await cache.SetAsync("scope", "a", new byte[10], CancellationToken.None);
        await cache.SetAsync("scope", "b", new byte[10], CancellationToken.None);

        // Touching "a" makes "b" the least recently used.
        Assert.NotNull(await cache.TryGetAsync("scope", "a", CancellationToken.None));

        await cache.SetAsync("scope", "c", new byte[10], CancellationToken.None);

        Assert.NotNull(await cache.TryGetAsync("scope", "a", CancellationToken.None));
        Assert.Null(await cache.TryGetAsync("scope", "b", CancellationToken.None));
        Assert.NotNull(await cache.TryGetAsync("scope", "c", CancellationToken.None));
    }

    [Fact]
    public async Task Memory_cache_declines_content_larger_than_its_whole_budget()
    {
        var cache = new MemoryComicCache(maxSizeInBytes: 10);

        await cache.SetAsync("scope", "huge", new byte[100], CancellationToken.None);

        Assert.Null(await cache.TryGetAsync("scope", "huge", CancellationToken.None));
    }

    [Fact]
    public async Task The_default_cache_stores_nothing()
    {
        var cache = NullComicCache.Instance;

        await cache.SetAsync("scope", "a", new byte[10], CancellationToken.None);

        Assert.Null(await cache.TryGetAsync("scope", "a", CancellationToken.None));
    }

    private static async Task<byte[]> ReadAsync(ComicPage page)
    {
        await using var stream = await page.OpenAsync();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);
        return buffer.ToArray();
    }

    public void Dispose() => _fixture.Dispose();
}
