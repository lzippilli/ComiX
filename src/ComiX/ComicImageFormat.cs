namespace ComiX;

/// <summary>
/// The image format of a page, identified from its file extension.
/// </summary>
/// <remarks>
/// The format is inferred from the extension, not from the content. Use <see cref="ComicValidator"/>
/// with <see cref="ComicValidationOptions.DeepScan"/> to verify that the content matches.
/// </remarks>
public enum ComicImageFormat
{
    /// <summary>Not a recognised image format.</summary>
    Unknown = 0,

    /// <summary>JPEG.</summary>
    Jpeg = 1,

    /// <summary>PNG.</summary>
    Png = 2,

    /// <summary>GIF.</summary>
    Gif = 3,

    /// <summary>WebP.</summary>
    WebP = 4,

    /// <summary>BMP.</summary>
    Bmp = 5,

    /// <summary>TIFF.</summary>
    Tiff = 6,

    /// <summary>AVIF.</summary>
    Avif = 7,
}
