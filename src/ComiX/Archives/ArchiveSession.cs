using SharpCompress.Archives;

namespace ComiX.Archives;

/// <summary>
/// An archive opened for a single sequential pass, together with whatever the pass had to create.
/// </summary>
/// <param name="Archive">The archive, positioned at its start.</param>
/// <param name="OwnedStream">
/// The stream to dispose with the session, or <see langword="null"/> when the stream belongs to the
/// caller and must be left open.
/// </param>
internal readonly record struct ArchiveSession(IArchive Archive, Stream? OwnedStream) : IDisposable
{
    public void Dispose()
    {
        Archive.Dispose();
        OwnedStream?.Dispose();
    }
}
