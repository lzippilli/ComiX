namespace ComiX.Internal;

/// <summary>
/// A write-only stream that discards content while recording the byte count and the leading bytes.
/// Used by deep validation to read an entry without retaining it in memory.
/// </summary>
internal sealed class SniffingSink : Stream
{
    private const int HeaderLength = 16;

    private readonly byte[] _header = new byte[HeaderLength];
    private int _headerLength;

    public long BytesWritten { get; private set; }

    public ReadOnlySpan<byte> Header => _header.AsSpan(0, _headerLength);

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length => BytesWritten;

    public override long Position
    {
        get => BytesWritten;
        set => throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count) =>
        Write(buffer.AsSpan(offset, count));

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (_headerLength < HeaderLength)
        {
            var take = Math.Min(HeaderLength - _headerLength, buffer.Length);
            buffer[..take].CopyTo(_header.AsSpan(_headerLength));
            _headerLength += take;
        }

        BytesWritten += buffer.Length;
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Write(buffer.Span);
        return ValueTask.CompletedTask;
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

    public override void Flush() { }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();
}
