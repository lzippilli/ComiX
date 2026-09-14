namespace ComiX.Internal;

/// <summary>
/// Maps file extensions to image formats and MIME types, and identifies formats from leading bytes.
/// </summary>
internal static class ImageFormats
{
    private static readonly Dictionary<string, ComicImageFormat> ByExtension =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = ComicImageFormat.Jpeg,
            [".jpeg"] = ComicImageFormat.Jpeg,
            [".jpe"] = ComicImageFormat.Jpeg,
            [".png"] = ComicImageFormat.Png,
            [".gif"] = ComicImageFormat.Gif,
            [".webp"] = ComicImageFormat.WebP,
            [".bmp"] = ComicImageFormat.Bmp,
            [".dib"] = ComicImageFormat.Bmp,
            [".tif"] = ComicImageFormat.Tiff,
            [".tiff"] = ComicImageFormat.Tiff,
            [".avif"] = ComicImageFormat.Avif,
        };

    public static ComicImageFormat FromFileName(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return extension.Length > 0 && ByExtension.TryGetValue(extension, out var format)
            ? format
            : ComicImageFormat.Unknown;
    }

    public static string? MimeTypeOf(ComicImageFormat format) =>
        format switch
        {
            ComicImageFormat.Jpeg => "image/jpeg",
            ComicImageFormat.Png => "image/png",
            ComicImageFormat.Gif => "image/gif",
            ComicImageFormat.WebP => "image/webp",
            ComicImageFormat.Bmp => "image/bmp",
            ComicImageFormat.Tiff => "image/tiff",
            ComicImageFormat.Avif => "image/avif",
            _ => null,
        };

    /// <summary>
    /// Identifies an image format from the leading bytes of its content, returning
    /// <see cref="ComicImageFormat.Unknown"/> when no signature matches.
    /// </summary>
    public static ComicImageFormat FromHeader(ReadOnlySpan<byte> header)
    {
        if (StartsWith(header, [0xFF, 0xD8, 0xFF]))
        {
            return ComicImageFormat.Jpeg;
        }

        if (StartsWith(header, [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return ComicImageFormat.Png;
        }

        if (StartsWith(header, "GIF87a"u8) || StartsWith(header, "GIF89a"u8))
        {
            return ComicImageFormat.Gif;
        }

        if (StartsWith(header, "BM"u8))
        {
            return ComicImageFormat.Bmp;
        }

        if (StartsWith(header, [0x49, 0x49, 0x2A, 0x00]) || StartsWith(header, [0x4D, 0x4D, 0x00, 0x2A]))
        {
            return ComicImageFormat.Tiff;
        }

        // RIFF and ISO base media containers declare their type after a size/box header.
        if (header.Length >= 12 && StartsWith(header, "RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8))
        {
            return ComicImageFormat.WebP;
        }

        if (header.Length >= 12 && header[4..8].SequenceEqual("ftyp"u8)
            && (header[8..12].SequenceEqual("avif"u8) || header[8..12].SequenceEqual("avis"u8)))
        {
            return ComicImageFormat.Avif;
        }

        return ComicImageFormat.Unknown;
    }

    private static bool StartsWith(ReadOnlySpan<byte> value, ReadOnlySpan<byte> prefix) =>
        value.Length >= prefix.Length && value[..prefix.Length].SequenceEqual(prefix);
}
