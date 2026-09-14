namespace ComiX;

/// <summary>Base class for all exceptions raised by ComiX.</summary>
public class ComiXException : Exception
{
    /// <summary>Initialises a new instance with the specified message.</summary>
    public ComiXException(string message)
        : base(message) { }

    /// <summary>Initialises a new instance with the specified message and inner exception.</summary>
    public ComiXException(string message, Exception innerException)
        : base(message, innerException) { }
}

/// <summary>Thrown when the container format is not recognised, or is recognised but unsupported.</summary>
public sealed class UnsupportedComicFormatException : ComiXException
{
    /// <summary>Initialises a new instance for the specified detected format.</summary>
    public UnsupportedComicFormatException(string message, ComicContainerFormat format)
        : base(message) => Format = format;

    /// <summary>The format that was detected, or <see cref="ComicContainerFormat.Unknown"/>.</summary>
    public ComicContainerFormat Format { get; }
}

/// <summary>
/// Thrown when an archive is malformed or unreadable, or a configured resource limit is exceeded.
/// </summary>
public sealed class ComicArchiveException : ComiXException
{
    /// <summary>Initialises a new instance with the specified message.</summary>
    public ComicArchiveException(string message)
        : base(message) { }

    /// <summary>Initialises a new instance with the specified message and inner exception.</summary>
    public ComicArchiveException(string message, Exception innerException)
        : base(message, innerException) { }

    /// <summary>Initialises a new instance describing a problem with a specific archive entry.</summary>
    public ComicArchiveException(string message, string entryName)
        : base(message) => EntryName = entryName;

    /// <summary>The archive entry the error relates to, when the error is entry-specific.</summary>
    public string? EntryName { get; }
}

/// <summary>Thrown when a metadata source cannot be parsed.</summary>
/// <remarks>
/// <see cref="ComicBook.OpenAsync(string, ComicOpenOptions, CancellationToken)"/> does not propagate
/// this exception. Parsing failures leave canonical metadata empty and are reported by
/// <see cref="ComicValidator"/> as warnings.
/// </remarks>
public sealed class ComicMetadataException : ComiXException
{
    /// <summary>Initialises a new instance with the specified message.</summary>
    public ComicMetadataException(string message)
        : base(message) { }

    /// <summary>Initialises a new instance with the specified message and inner exception.</summary>
    public ComicMetadataException(string message, Exception innerException)
        : base(message, innerException) { }
}
