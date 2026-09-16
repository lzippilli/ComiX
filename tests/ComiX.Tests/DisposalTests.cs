using ComiX.Tests.Fixtures;
using Xunit;

namespace ComiX.Tests;

/// <summary>
/// The disposal contract: what happens to reads in progress, queued and requested afterwards.
/// </summary>
public sealed class DisposalTests : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly ComicFixture _fixture = new();

    [Fact]
    public async Task DisposeAsync_waits_for_the_read_in_progress()
    {
        var comic = await ComicBook.OpenAsync(_fixture.CreateStandardComic());
        var destination = new BlockingStream();

        var read = comic.Pages[0].ExtractAsync(destination);
        await destination.Started.WaitAsync(Timeout);

        var dispose = comic.DisposeAsync().AsTask();
        await Task.Delay(200);
        Assert.False(dispose.IsCompleted);

        destination.Release();

        await read.WaitAsync(Timeout);
        await dispose.WaitAsync(Timeout);
        Assert.Equal(TestImages.Jpeg(1), destination.Written);
    }

    [Fact]
    public async Task A_read_queued_after_DisposeAsync_throws_ObjectDisposedException()
    {
        var comic = await ComicBook.OpenAsync(_fixture.CreateStandardComic());
        var destination = new BlockingStream();

        var inProgress = comic.Pages[0].ExtractAsync(destination);
        await destination.Started.WaitAsync(Timeout);

        var dispose = comic.DisposeAsync().AsTask();
        var queued = comic.Pages[1].ExtractAsync(new MemoryStream());

        destination.Release();

        await inProgress.WaitAsync(Timeout);
        await dispose.WaitAsync(Timeout);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => queued.WaitAsync(Timeout));
    }

    [Fact]
    public async Task Dispose_lets_the_read_in_progress_complete_and_releases_the_file_afterwards()
    {
        var path = _fixture.CreateStandardComic();
        var comic = await ComicBook.OpenAsync(path);
        var destination = new BlockingStream();

        var read = comic.Pages[0].ExtractAsync(destination);
        await destination.Started.WaitAsync(Timeout);

        comic.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => comic.Pages[1].OpenAsync());

        destination.Release();
        await read.WaitAsync(Timeout);
        Assert.Equal(TestImages.Jpeg(1), destination.Written);

        // The file handle is released once the read has finished, without the caller waiting for it.
        await WaitUntilAsync(() => TryDelete(path));
    }

    [Fact]
    public async Task Reads_after_disposal_throw_ObjectDisposedException()
    {
        var path = _fixture.CreateStandardComic("comic.cbz", "<ComicInfo><Series>Batman</Series></ComicInfo>");
        var comic = await ComicBook.OpenAsync(path);
        var page = comic.Pages[0];
        var resource = Assert.Single(comic.Resources);

        await comic.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => page.OpenAsync());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => page.ExtractAsync(new MemoryStream()));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => page.ExtractAsync(_fixture.PathFor("out.jpg")));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => resource.OpenAsync());
        await Assert.ThrowsAsync<ObjectDisposedException>(() => comic.ExtractAllAsync(_fixture.PathFor("out")));
    }

    [Fact]
    public async Task Reads_after_disposal_throw_even_when_the_content_is_cached()
    {
        var options = new ComicOpenOptions { Cache = ComicCache.InMemory() };
        var comic = await ComicBook.OpenAsync(_fixture.CreateStandardComic(), options);

        await using (await comic.Pages[0].OpenAsync())
        {
        }

        await comic.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => comic.Pages[0].OpenAsync());
    }

    [Fact]
    public async Task Streams_returned_before_disposal_remain_readable()
    {
        var comic = await ComicBook.OpenAsync(_fixture.CreateStandardComic());
        var content = await comic.Pages[0].OpenAsync();

        await comic.DisposeAsync();

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer);
        Assert.Equal(TestImages.Jpeg(1), buffer.ToArray());
    }

    [Fact]
    public async Task Both_disposal_paths_are_idempotent()
    {
        var first = await ComicBook.OpenAsync(_fixture.CreateStandardComic("first.cbz"));
        first.Dispose();
        await first.DisposeAsync();
        first.Dispose();

        var second = await ComicBook.OpenAsync(_fixture.CreateStandardComic("second.cbz"));
        await second.DisposeAsync();
        second.Dispose();
        await second.DisposeAsync();
    }

    public void Dispose() => _fixture.Dispose();

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The condition was not met within the timeout.");
            await Task.Delay(50);
        }
    }

    private static bool TryDelete(string path)
    {
        try
        {
            File.Delete(path);
            return true;
        }
        catch (IOException)
        {
            // Still held open by the archive.
            return false;
        }
    }

    /// <summary>
    /// A write-only stream whose first write blocks until released, holding a copy in progress at
    /// a known point.
    /// </summary>
    private sealed class BlockingStream : Stream
    {
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly MemoryStream _written = new();

        public Task Started => _started.Task;

        public byte[] Written => _written.ToArray();

        public void Release() => _release.TrySetResult();

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            _started.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken);
            _written.Write(buffer.Span);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override void Write(byte[] buffer, int offset, int count) =>
            WriteAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => _written.Length;

        public override long Position
        {
            get => _written.Position;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
