namespace ComiX.Cache;

/// <summary>The default implementation, which stores nothing. Caching is opt-in.</summary>
internal sealed class NullComicCache : IComicCache
{
    public static NullComicCache Instance { get; } = new();

    private NullComicCache() { }

    public ValueTask<byte[]?> TryGetAsync(string scope, string key, CancellationToken cancellationToken) =>
        ValueTask.FromResult<byte[]?>(null);

    public ValueTask SetAsync(string scope, string key, byte[] content, CancellationToken cancellationToken) =>
        ValueTask.CompletedTask;
}
