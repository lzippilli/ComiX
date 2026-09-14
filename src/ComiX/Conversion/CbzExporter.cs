using ComiX.Archives;
using ComiX.Internal;
using SharpCompress.Common;
using SharpCompress.Writers;
using SharpCompress.Writers.Zip;

namespace ComiX.Conversion;

/// <summary>
/// Writes a comic's entries into a new CBZ, one entry at a time.
/// </summary>
/// <remarks>
/// Export operates on the common model rather than on a specific source container, so every
/// supported format converts through the same code path. Entries are copied individually; memory
/// consumption does not scale with archive size.
/// </remarks>
internal static class CbzExporter
{
    public static async Task ExportAsync(
        IReadOnlyList<ComicArchiveEntry> entries,
        ComicContentReader reader,
        string? archiveComment,
        string destinationPath,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(destinationPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var destination = new FileStream(
            fullPath,
            overwrite ? FileMode.Create : FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.Asynchronous);

        try
        {
            await using (destination.ConfigureAwait(false))
            {
                var writerOptions = new ZipWriterOptions(CompressionType.Deflate)
                {
                    LeaveStreamOpen = true,
                    ArchiveComment = archiveComment,
                };

                using var writer = WriterFactory.Open(destination, ArchiveType.Zip, writerOptions);

                foreach (var entry in entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var content = await reader.OpenAsync(entry, cancellationToken).ConfigureAwait(false);
                    await using (content.ConfigureAwait(false))
                    {
                        await writer.WriteAsync(entry.Key, content, entry.LastModified, cancellationToken)
                            .ConfigureAwait(false);
                    }
                }
            }
        }
        catch
        {
            // A partially written archive would be indistinguishable from a valid one.
            TryDelete(fullPath);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // The original failure is more relevant than a cleanup error.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
