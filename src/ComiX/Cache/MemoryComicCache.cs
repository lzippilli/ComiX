namespace ComiX.Cache;

/// <summary>
/// A size-bounded, least-recently-used in-memory cache of extracted entry content.
/// </summary>
/// <remarks>
/// Content larger than the configured budget is not stored, so a single large entry cannot evict the
/// entire cache. All members may be called concurrently.
/// </remarks>
internal sealed class MemoryComicCache : IComicCache
{
    private readonly long _maxSizeInBytes;
#if NET9_0_OR_GREATER
    private readonly Lock _sync = new();
#else
    private readonly object _sync = new();
#endif
    private readonly Dictionary<string, LinkedListNode<CacheItem>> _items = new(StringComparer.Ordinal);
    private readonly LinkedList<CacheItem> _recency = new();
    private long _currentSize;

    public MemoryComicCache(long maxSizeInBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxSizeInBytes);
        _maxSizeInBytes = maxSizeInBytes;
    }

    public ValueTask<byte[]?> TryGetAsync(string scope, string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var cacheKey = BuildKey(scope, key);

        lock (_sync)
        {
            if (!_items.TryGetValue(cacheKey, out var node))
            {
                return ValueTask.FromResult<byte[]?>(null);
            }

            _recency.Remove(node);
            _recency.AddFirst(node);
            return ValueTask.FromResult<byte[]?>(node.Value.Content);
        }
    }

    public ValueTask SetAsync(string scope, string key, byte[] content, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (content.LongLength > _maxSizeInBytes)
        {
            return ValueTask.CompletedTask;
        }

        var cacheKey = BuildKey(scope, key);

        lock (_sync)
        {
            if (_items.TryGetValue(cacheKey, out var existing))
            {
                _currentSize -= existing.Value.Content.LongLength;
                _recency.Remove(existing);
                _items.Remove(cacheKey);
            }

            var node = _recency.AddFirst(new CacheItem(cacheKey, content));
            _items[cacheKey] = node;
            _currentSize += content.LongLength;

            while (_currentSize > _maxSizeInBytes && _recency.Last is { } oldest)
            {
                _recency.RemoveLast();
                _items.Remove(oldest.Value.Key);
                _currentSize -= oldest.Value.Content.LongLength;
            }
        }

        return ValueTask.CompletedTask;
    }

    // NUL is used as the separator because it cannot occur in a path or an archive entry name.
    private static string BuildKey(string scope, string key) => $"{scope}\0{key}";

    private sealed record CacheItem(string Key, byte[] Content);
}
