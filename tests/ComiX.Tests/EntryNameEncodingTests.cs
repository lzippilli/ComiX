using System.Text;
using ComiX.Tests.Fixtures;
using Xunit;

namespace ComiX.Tests;

/// <summary>
/// How entry names are decoded, and how a consumer corrects an archive whose names were written in
/// an encoding it does not declare.
/// </summary>
public sealed class EntryNameEncodingTests : IDisposable
{
    private static readonly Encoding Latin1 = CreateLatin1();

    private readonly ComicFixture _fixture = new();

    [Fact]
    public void Ascii_names_decode_identically_under_every_encoding()
    {
        // The common case: no archive with ASCII names is affected by any of this.
        var utf8 = CreateComic("ascii-utf8.cbz", "001.jpg", null);
        var legacy = CreateComic("ascii-legacy.cbz", "001.jpg", Latin1);

        Assert.Equal("001.jpg", NameOf(utf8, null));
        Assert.Equal("001.jpg", NameOf(legacy, null));
        Assert.Equal("001.jpg", NameOf(legacy, Latin1));
    }

    [Fact]
    public void A_name_in_an_undeclared_encoding_decodes_with_replacement_characters()
    {
        var path = CreateComic("legacy.cbz", "niño-01.jpg", Latin1);

        var name = NameOf(path, null);

        Assert.Contains('\uFFFD', name);
        Assert.NotEqual("niño-01.jpg", name);
    }

    [Fact]
    public void Setting_the_encoding_decodes_the_name_correctly()
    {
        var path = CreateComic("legacy.cbz", "niño-01.jpg", Latin1);

        Assert.Equal("niño-01.jpg", NameOf(path, Latin1));
    }

    [Fact]
    public async Task Setting_the_encoding_does_not_affect_names_declared_as_utf8()
    {
        // The option sets ArchiveEncoding.Default, not Forced, so a declared UTF-8 name is untouched.
        // The archive is written with System.IO.Compression, which sets the UTF-8 flag; the
        // SharpCompress writer does not.
        var path = CreateUtf8DeclaringComic("declared-utf8.cbz", "niño-01.jpg");

        await using var comic = await ComicBook.OpenAsync(
            path,
            new ComicOpenOptions { EntryNameEncoding = Latin1 });

        Assert.Equal("niño-01.jpg", comic.Pages[0].Name);
    }

    [Fact]
    public async Task Exported_archives_read_their_non_ascii_names_back_unchanged()
    {
        var source = CreateUtf8DeclaringComic("source.cbz", "niño-01.jpg");
        var destination = _fixture.PathFor("exported.cbz");

        await using (var comic = await ComicBook.OpenAsync(source))
        {
            await comic.ExportAsync(destination);
        }

        await using var exported = await ComicBook.OpenAsync(destination);

        Assert.Equal("niño-01.jpg", exported.Pages[0].Name);
    }

    [Fact]
    public async Task Page_order_follows_the_corrected_names()
    {
        var entries = new[]
        {
            ("página-10.jpg", TestImages.Jpeg(10)),
            ("página-2.jpg", TestImages.Jpeg(2)),
            ("página-1.jpg", TestImages.Jpeg(1)),
        };
        var path = _fixture.CreateZip("order.cbz", entries, entryNameEncoding: Latin1);

        await using var comic = await ComicBook.OpenAsync(
            path,
            new ComicOpenOptions { EntryNameEncoding = Latin1 });

        Assert.Equal(
            ["página-1.jpg", "página-2.jpg", "página-10.jpg"],
            comic.Pages.Select(page => page.Name));
    }

    [Fact]
    public async Task Validation_reports_a_suspect_entry_name_and_names_the_option()
    {
        var path = CreateComic("legacy.cbz", "niño-01.jpg", Latin1);

        var result = await ComicValidator.ValidateAsync(path);

        Assert.True(result.IsValid);
        var warning = Assert.Single(
            result.Warnings,
            issue => issue.Code is ComicValidationCode.EntryNameEncodingSuspect);
        Assert.Contains(nameof(ComicOpenOptions.EntryNameEncoding), warning.Message);
    }

    [Fact]
    public async Task Validation_reports_nothing_once_the_encoding_is_supplied()
    {
        var path = CreateComic("legacy.cbz", "niño-01.jpg", Latin1);

        var result = await ComicValidator.ValidateAsync(
            path,
            new ComicValidationOptions { EntryNameEncoding = Latin1 });

        Assert.DoesNotContain(
            result.Issues,
            issue => issue.Code is ComicValidationCode.EntryNameEncodingSuspect);
    }

    public void Dispose() => _fixture.Dispose();

    private string CreateComic(string fileName, string pageName, Encoding? encoding) =>
        _fixture.CreateZip(fileName, [(pageName, TestImages.Jpeg(1))], entryNameEncoding: encoding);

    /// <summary>
    /// Writes an archive that sets the ZIP UTF-8 flag, which the SharpCompress writer used elsewhere
    /// in these fixtures does not.
    /// </summary>
    private string CreateUtf8DeclaringComic(string fileName, string pageName)
    {
        var path = _fixture.PathFor(fileName);
        using var file = File.Create(path);
        using var zip = new System.IO.Compression.ZipArchive(file, System.IO.Compression.ZipArchiveMode.Create);
        using var entry = zip.CreateEntry(pageName).Open();
        entry.Write(TestImages.Jpeg(1));
        return path;
    }

    private static string NameOf(string path, Encoding? encoding)
    {
        using var comic = ComicBook.OpenAsync(path, new ComicOpenOptions { EntryNameEncoding = encoding })
            .GetAwaiter()
            .GetResult();

        return comic.Pages[0].Name;
    }

    /// <summary>
    /// Legacy code pages are not available on .NET targets until the provider is registered, which
    /// is the consumer's responsibility rather than the library's.
    /// </summary>
    private static Encoding CreateLatin1()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252);
    }
}
