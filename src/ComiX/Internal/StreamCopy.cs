namespace ComiX.Internal;

/// <summary>
/// Shared constants for copying archive content.
/// </summary>
/// <remarks>
/// Size limits are not applied here: they belong to <see cref="LimitedStream"/>, which
/// <see cref="ComicContentReader"/> wraps around every destination.
/// </remarks>
internal static class StreamCopy
{
    /// <summary>The buffer size used when copying entry content.</summary>
    public const int BufferSize = 81920;
}
