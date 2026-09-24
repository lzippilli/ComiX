using SharpCompress.Archives;

namespace ComiX.Archives;

/// <summary>
/// Enumerates an archive's entries in stored order, keeping the archive open between reads so that
/// the position reached by one read is available to the next.
/// </summary>
/// <remarks>
/// <see cref="Reset"/> releases the archive and starts again from the first entry, which for a solid
/// container is the only way to reach an entry already passed.
/// </remarks>
internal sealed class ArchiveEnumerator : IEnumerator<IArchiveEntry>
{
    private readonly Func<ArchiveSession> _open;
    private readonly int _count;
    private readonly HashSet<string> _passed = new(StringComparer.Ordinal);

    private ArchiveSession? _session;
    private IEnumerator<IArchiveEntry>? _entries;
    private IArchiveEntry? _current;

    /// <param name="open">Opens the archive, positioned at its first entry.</param>
    /// <param name="count">How many entries the enumeration returns before it ends.</param>
    public ArchiveEnumerator(Func<ArchiveSession> open, int count)
    {
        _open = open;
        _count = count;
    }

    /// <summary>The entry at the current position.</summary>
    /// <exception cref="InvalidOperationException">The enumerator is not positioned on an entry.</exception>
    public IArchiveEntry Current =>
        _current ?? throw new InvalidOperationException("The enumerator is not positioned on an entry.");

    object System.Collections.IEnumerator.Current => Current;

    /// <summary>Whether the enumeration has already returned the entry with this key.</summary>
    public bool HasPassed(string key) => _passed.Contains(key);

    /// <summary>Whether the enumeration is positioned before the first entry.</summary>
    public bool AtStart => _passed.Count == 0;

    /// <summary>
    /// Whether every entry has been returned, so that nothing further can be read without starting
    /// again. A count that disagrees with the archive costs a traversal, never correctness.
    /// </summary>
    public bool AtEnd => _passed.Count >= _count;

    /// <summary>Advances to the next content entry, opening the archive on first use.</summary>
    /// <returns><see langword="false"/> once the last entry has been passed.</returns>
    public bool MoveNext()
    {
        _entries ??= Open();

        while (_entries.MoveNext())
        {
            var entry = _entries.Current;
            if (entry.IsDirectory || entry.Key is not { Length: > 0 } key)
            {
                continue;
            }

            _current = entry;
            _passed.Add(ArchiveIndex.Normalise(key));
            return true;
        }

        _current = null;
        return false;
    }

    /// <summary>
    /// Returns to the position before the first entry, releasing the archive. The next
    /// <see cref="MoveNext"/> opens it again.
    /// </summary>
    public void Reset()
    {
        Release();
        _passed.Clear();
    }

    public void Dispose() => Release();

    private IEnumerator<IArchiveEntry> Open()
    {
        var session = _open();
        _session = session;
        return session.Archive.Entries.GetEnumerator();
    }

    private void Release()
    {
        _entries?.Dispose();
        _entries = null;
        _current = null;

        _session?.Dispose();
        _session = null;
    }
}
