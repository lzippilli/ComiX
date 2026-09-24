namespace ComiX.Archives;

/// <summary>
/// Copies the content of one archive entry to a destination stream.
/// </summary>
/// <remarks>
/// The implementation determines how the entry is accessed and may use direct or sequential access
/// depending on the capabilities of the archive. An implementation may hold an archive open between
/// reads, so <see cref="IDisposable.Dispose"/> is part of the contract.
/// </remarks>
internal interface IEntryAccess : IDisposable
{
    ValueTask CopyEntryToAsync(ComicArchiveEntry entry, Stream destination, CancellationToken cancellationToken);
}
