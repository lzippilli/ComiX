namespace ComiX.Internal;

/// <summary>
/// Passes writes through to another stream until a byte budget is exceeded, then fails.
/// </summary>
/// <remarks>
/// Applied by <see cref="ComicContentReader"/> to every content read, so the limit applies uniformly
/// to all container formats. The limit is evaluated against the bytes produced, not against the size
/// declared by the archive, which is what makes it effective against decompression bombs.
/// </remarks>
internal sealed class LimitedStream : Stream
{
    private readonly Stream _destination;
    private readonly long _maxBytes;
    private readonly string _entryName;
    private long _written;

    public LimitedStream(Stream destination, long maxBytes, string entryName)
    {
        _destination = destination;
        _maxBytes = maxBytes;
        _entryName = entryName;
    }

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => _written;

    public override long Position
    {
        get => _written;
        set => throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        Account(count);
        _destination.Write(buffer, offset, count);
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        Account(buffer.Length);
        _destination.Write(buffer);
    }

    public override async ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        Account(buffer.Length);
        await _destination.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override void Flush() => _destination.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) =>
        _destination.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        // The destination belongs to the caller.
        base.Dispose(disposing);
    }

    private void Account(int count)
    {
        _written += count;
        if (_written > _maxBytes)
        {
            throw new ComicArchiveException(
                $"Entry '{_entryName}' exceeds the configured limit of {_maxBytes} bytes and was not read fully.",
                _entryName);
        }
    }
}
