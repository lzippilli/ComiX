using System.Text;
using SharpCompress.Common;
using SharpCompress.Writers;
using SharpCompress.Writers.Zip;

namespace ComiX.Tests.Fixtures;

/// <summary>
/// Builds small comic archives at test time so the repository carries no binary fixtures.
/// </summary>
internal sealed class ComicFixture : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "comix-tests",
        Guid.NewGuid().ToString("N"));

    public ComicFixture() => Directory.CreateDirectory(_root);

    public string Root => _root;

    public string PathFor(string fileName) => Path.Combine(_root, fileName);

    /// <summary>Writes a ZIP-based comic containing the given entries and returns its path.</summary>
    /// <param name="fileName">The archive file name within the fixture directory.</param>
    /// <param name="entries">The entries to write.</param>
    /// <param name="archiveComment">The archive-level comment, where the test needs one.</param>
    /// <param name="entryNameEncoding">
    /// The encoding for entry names. When supplied and not UTF-8, the archive stores the names in
    /// that encoding without declaring it, which is how legacy writers produce them.
    /// </param>
    public string CreateZip(
        string fileName,
        IEnumerable<(string Name, byte[] Content)> entries,
        string? archiveComment = null,
        Encoding? entryNameEncoding = null)
    {
        var path = PathFor(fileName);
        using var file = File.Create(path);
        var options = new ZipWriterOptions(CompressionType.Deflate)
        {
            LeaveStreamOpen = true,
            ArchiveComment = archiveComment,
        };

        if (entryNameEncoding is not null)
        {
            options.ArchiveEncoding.Default = entryNameEncoding;
        }

        using (var writer = WriterFactory.Open(file, ArchiveType.Zip, options))
        {
            foreach (var (name, content) in entries)
            {
                using var source = new MemoryStream(content);
                writer.Write(name, source, DateTime.UtcNow);
            }
        }

        return path;
    }

    /// <summary>Writes a TAR-based comic, used to prove format detection does not rely on extensions.</summary>
    public string CreateTar(string fileName, IEnumerable<(string Name, byte[] Content)> entries)
    {
        var path = PathFor(fileName);
        using var file = File.Create(path);
        using (var writer = WriterFactory.Open(file, ArchiveType.Tar, new WriterOptions(CompressionType.None)
        {
            LeaveStreamOpen = true,
        }))
        {
            foreach (var (name, content) in entries)
            {
                using var source = new MemoryStream(content);
                writer.Write(name, source, DateTime.UtcNow);
            }
        }

        return path;
    }

    /// <summary>Writes a file with the given raw bytes, for headers that cannot be produced by a writer.</summary>
    public string CreateRaw(string fileName, byte[] content)
    {
        var path = PathFor(fileName);
        File.WriteAllBytes(path, content);
        return path;
    }

    /// <summary>
    /// Writes one of the committed binary fixtures to disk and returns its path.
    /// </summary>
    /// <remarks>
    /// RAR and 7-Zip archives cannot be produced from .NET, so those two fixtures ship as embedded
    /// resources. Each contains the same content as <see cref="CreateStandardComic"/> plus a
    /// ComicInfo.xml, so the same expectations apply to every container.
    /// </remarks>
    public string ExtractAsset(string assetName)
    {
        var assembly = typeof(ComicFixture).Assembly;
        var resourceName = Array.Find(
                assembly.GetManifestResourceNames(),
                name => name.EndsWith(assetName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"The fixture '{assetName}' is not embedded in the test assembly.");

        using var resource = assembly.GetManifestResourceStream(resourceName)!;
        var path = PathFor(assetName);
        using var file = File.Create(path);
        resource.CopyTo(file);
        return path;
    }

    /// <summary>A comic with three pages named so that naive alphabetical ordering gets them wrong.</summary>
    public string CreateStandardComic(string fileName = "comic.cbz", string? comicInfo = null)
    {
        var entries = new List<(string, byte[])>
        {
            ("1.jpg", TestImages.Jpeg(1)),
            ("2.jpg", TestImages.Jpeg(2)),
            ("10.jpg", TestImages.Jpeg(10)),
        };

        if (comicInfo is not null)
        {
            entries.Add(("ComicInfo.xml", System.Text.Encoding.UTF8.GetBytes(comicInfo)));
        }

        return CreateZip(fileName, entries);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
            // A locked temp file must not fail a test run.
        }
    }
}
