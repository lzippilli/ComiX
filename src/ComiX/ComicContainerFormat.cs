namespace ComiX;

/// <summary>
/// The container format a comic is stored in, determined by inspecting its content rather than its
/// file extension.
/// </summary>
public enum ComicContainerFormat
{
    /// <summary>The container could not be recognised.</summary>
    Unknown = 0,

    /// <summary>A ZIP container, conventionally named <c>.cbz</c>.</summary>
    Cbz = 1,

    /// <summary>A RAR container, conventionally named <c>.cbr</c>.</summary>
    Cbr = 2,

    /// <summary>A 7-Zip container, conventionally named <c>.cb7</c>.</summary>
    Cb7 = 3,

    /// <summary>A TAR container, conventionally named <c>.cbt</c>.</summary>
    Cbt = 4,
}
