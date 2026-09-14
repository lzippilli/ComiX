using System.Text;

namespace ComiX.Archives;

/// <summary>
/// Reads the archive-level comment of a ZIP file, which is where the ComicBookInfo standard stores
/// its metadata. SharpCompress writes this field but does not expose it when reading.
/// </summary>
/// <remarks>
/// The end-of-central-directory record is the final structure in a ZIP file: a 22-byte header whose
/// last field is the comment length, followed by the comment. The search is bounded because the
/// comment cannot exceed 64 KiB.
/// </remarks>
internal static class ZipArchiveComment
{
    private const int EndOfCentralDirectorySize = 22;
    private const int MaxCommentLength = ushort.MaxValue;
    private static readonly byte[] Signature = [0x50, 0x4B, 0x05, 0x06];

    /// <summary>
    /// Returns the archive comment, or <see langword="null"/> when the file has none or the record
    /// cannot be found. The stream's position is restored before returning.
    /// </summary>
    public static string? Read(Stream stream)
    {
        if (!stream.CanSeek)
        {
            return null;
        }

        var origin = stream.Position;
        try
        {
            var searchLength = (int)Math.Min(stream.Length, EndOfCentralDirectorySize + MaxCommentLength);
            if (searchLength < EndOfCentralDirectorySize)
            {
                return null;
            }

            var buffer = new byte[searchLength];
            stream.Position = stream.Length - searchLength;
            stream.ReadExactly(buffer);

            var record = FindSignature(buffer);
            if (record < 0)
            {
                return null;
            }

            var commentLength = BitConverter.ToUInt16(buffer, record + 20);
            var commentStart = record + EndOfCentralDirectorySize;
            if (commentLength == 0 || commentStart + commentLength > buffer.Length)
            {
                return null;
            }

            // The ZIP specification does not define the comment encoding. UTF-8 is assumed, and
            // invalid byte sequences are replaced rather than throwing.
            return Encoding.UTF8.GetString(buffer, commentStart, commentLength);
        }
        catch (IOException)
        {
            return null;
        }
        finally
        {
            stream.Position = origin;
        }
    }

    /// <summary>
    /// Finds the last end-of-central-directory signature. The search runs backwards because the
    /// four signature bytes can also occur within compressed data earlier in the file.
    /// </summary>
    private static int FindSignature(ReadOnlySpan<byte> buffer)
    {
        for (var i = buffer.Length - EndOfCentralDirectorySize; i >= 0; i--)
        {
            if (buffer.Slice(i, Signature.Length).SequenceEqual(Signature))
            {
                return i;
            }
        }

        return -1;
    }
}
