namespace ComiX.Detection;

/// <summary>
/// Recognises container formats from the first bytes of a file.
/// </summary>
/// <remarks>
/// Formats are identified by magic bytes, except TAR, which defines none at offset zero and is
/// identified by its <c>ustar</c> marker or, for pre-POSIX archives, by validating the checksum of
/// the first header block.
/// </remarks>
internal static class ContainerSignature
{
    /// <summary>How many bytes must be read before <see cref="Identify"/> can decide.</summary>
    public const int HeaderLength = 512;

    public static ComicContainerFormat Identify(ReadOnlySpan<byte> header)
    {
        if (StartsWith(header, [0x50, 0x4B, 0x03, 0x04])
            || StartsWith(header, [0x50, 0x4B, 0x05, 0x06])
            || StartsWith(header, [0x50, 0x4B, 0x07, 0x08]))
        {
            return ComicContainerFormat.Cbz;
        }

        if (StartsWith(header, [0x52, 0x61, 0x72, 0x21, 0x1A, 0x07]))
        {
            return ComicContainerFormat.Cbr;
        }

        if (StartsWith(header, [0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C]))
        {
            return ComicContainerFormat.Cb7;
        }

        return IsTar(header) ? ComicContainerFormat.Cbt : ComicContainerFormat.Unknown;
    }

    /// <summary>The extension conventionally used for a format, including the leading dot.</summary>
    public static string? ConventionalExtension(ComicContainerFormat format) =>
        format switch
        {
            ComicContainerFormat.Cbz => ".cbz",
            ComicContainerFormat.Cbr => ".cbr",
            ComicContainerFormat.Cb7 => ".cb7",
            ComicContainerFormat.Cbt => ".cbt",
            _ => null,
        };

    private static bool IsTar(ReadOnlySpan<byte> header)
    {
        if (header.Length < HeaderLength)
        {
            return false;
        }

        if (header[257..262].SequenceEqual("ustar"u8))
        {
            return true;
        }

        // Pre-POSIX tar has no marker, so the header block's own checksum is the only evidence.
        var declared = ParseOctal(header[148..156]);
        if (declared is null)
        {
            return false;
        }

        var sum = 0;
        for (var i = 0; i < HeaderLength; i++)
        {
            sum += i is >= 148 and < 156 ? ' ' : header[i];
        }

        return sum == declared && header[0] != 0;
    }

    private static int? ParseOctal(ReadOnlySpan<byte> field)
    {
        var value = 0;
        var digits = 0;
        foreach (var b in field)
        {
            if (b is (byte)' ' or 0)
            {
                if (digits > 0)
                {
                    break;
                }

                continue;
            }

            if (b is < (byte)'0' or > (byte)'7')
            {
                return null;
            }

            value = (value * 8) + (b - '0');
            digits++;
        }

        return digits == 0 ? null : value;
    }

    private static bool StartsWith(ReadOnlySpan<byte> value, ReadOnlySpan<byte> prefix) =>
        value.Length >= prefix.Length && value[..prefix.Length].SequenceEqual(prefix);
}
