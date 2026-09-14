using ComiX.Detection;

namespace ComiX.Internal;

/// <summary>
/// Reads the leading bytes required to identify a container, restoring the stream position.
/// </summary>
internal static class HeaderReader
{
    public static async Task<byte[]> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        var origin = stream.Position;
        stream.Position = 0;

        var buffer = new byte[ContainerSignature.HeaderLength];
        var read = await stream
            .ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, cancellationToken)
            .ConfigureAwait(false);

        stream.Position = origin;
        return read == buffer.Length ? buffer : buffer[..read];
    }
}
