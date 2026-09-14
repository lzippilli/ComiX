using ComiX.Tests.Fixtures;
using Xunit;

namespace ComiX.Tests;

/// <summary>
/// The same expectations applied to every container, because the point of ComiX is that a consumer
/// cannot tell which one they are holding.
/// </summary>
public sealed class ContainerTests : IDisposable
{
    private const string ComicInfo = """
        <?xml version="1.0" encoding="utf-8"?>
        <ComicInfo>
          <Series>Batman</Series>
          <Number>7</Number>
          <Writer>Alan Moore</Writer>
          <PageCount>3</PageCount>
        </ComicInfo>
        """;

    private readonly ComicFixture _fixture = new();

    public static TheoryData<string, ComicContainerFormat> Containers => new()
    {
        { "cbz", ComicContainerFormat.Cbz },
        { "cbt", ComicContainerFormat.Cbt },
        { "cbr", ComicContainerFormat.Cbr },
        { "cb7", ComicContainerFormat.Cb7 },
    };

    [Theory]
    [MemberData(nameof(Containers))]
    public async Task Detects_every_container(string kind, ComicContainerFormat expected)
    {
        var path = CreateComic(kind);

        var result = await ComicDetector.DetectAsync(path);

        Assert.Equal(expected, result.Format);
        Assert.True(result.IsSupported);
        Assert.Equal(3, result.PageCount);
        Assert.True(result.HasMetadata);
        Assert.Equal(ComicMetadataStandard.ComicInfo, result.MetadataStandard);
    }

    [Theory]
    [MemberData(nameof(Containers))]
    public async Task Opens_every_container_into_the_same_model(string kind, ComicContainerFormat expected)
    {
        var path = CreateComic(kind);

        await using var comic = await ComicBook.OpenAsync(path);

        Assert.Equal(expected, comic.Format);
        Assert.Equal(["1.jpg", "2.jpg", "10.jpg"], comic.Pages.Select(page => page.Name));
        Assert.Equal("Batman", comic.Metadata.Series);
        Assert.Equal("7", comic.Metadata.Number);
        Assert.Equal(new ComicCredit("Alan Moore", ComicCreditRole.Writer), Assert.Single(comic.Metadata.Credits));
        Assert.Equal("ComicInfo.xml", Assert.Single(comic.Resources).FileName);
    }

    [Theory]
    [MemberData(nameof(Containers))]
    public async Task Reads_page_content_from_every_container(string kind, ComicContainerFormat expected)
    {
        _ = expected;
        var path = CreateComic(kind);

        await using var comic = await ComicBook.OpenAsync(path);
        await using var page = await comic.Pages[1].OpenAsync();
        using var buffer = new MemoryStream();
        await page.CopyToAsync(buffer);

        Assert.Equal(TestImages.Jpeg(2), buffer.ToArray());
    }

    [Theory]
    [MemberData(nameof(Containers))]
    public async Task Validates_every_container(string kind, ComicContainerFormat expected)
    {
        _ = expected;
        var path = CreateComic(kind);

        var result = await ComicValidator.ValidateAsync(path, new ComicValidationOptions { DeepScan = true });

        Assert.True(result.IsValid);
        Assert.Equal(3, result.PageCount);
        Assert.Empty(result.Issues);
    }

    [Theory]
    [MemberData(nameof(Containers))]
    public async Task Converts_every_container_to_cbz_without_changing_the_comic(
        string kind,
        ComicContainerFormat expected)
    {
        _ = expected;
        var source = CreateComic(kind);
        var destination = _fixture.PathFor($"converted-from-{kind}.cbz");

        await using (var comic = await ComicBook.OpenAsync(source))
        {
            await comic.ExportAsync(destination);
        }

        await using var converted = await ComicBook.OpenAsync(destination);

        Assert.Equal(ComicContainerFormat.Cbz, converted.Format);
        Assert.Equal(["1.jpg", "2.jpg", "10.jpg"], converted.Pages.Select(page => page.Name));
        Assert.Equal("Batman", converted.Metadata.Series);
        Assert.Equal("7", converted.Metadata.Number);

        await using var page = await converted.Pages[2].OpenAsync();
        using var buffer = new MemoryStream();
        await page.CopyToAsync(buffer);
        Assert.Equal(TestImages.Jpeg(10), buffer.ToArray());
    }

    [Theory]
    [MemberData(nameof(Containers))]
    public async Task Extracts_from_every_container(string kind, ComicContainerFormat expected)
    {
        _ = expected;
        var path = CreateComic(kind);
        var destination = Path.Combine(_fixture.Root, $"out-{kind}");

        await using var comic = await ComicBook.OpenAsync(path);
        await comic.ExtractAllAsync(destination);

        Assert.Equal(TestImages.Jpeg(1), await File.ReadAllBytesAsync(Path.Combine(destination, "1.jpg")));
        Assert.True(File.Exists(Path.Combine(destination, "ComicInfo.xml")));
    }

    [Fact]
    public async Task Reports_solid_archives_as_not_supporting_cheap_random_access()
    {
        // The committed 7-Zip fixture is solid, which is the common case for that format.
        var solid = _fixture.ExtractAsset("sample.cb7");
        var notSolid = _fixture.ExtractAsset("sample.cbr");

        await using var sevenZip = await ComicBook.OpenAsync(solid);
        await using var rar = await ComicBook.OpenAsync(notSolid);

        Assert.False(sevenZip.Capabilities.HasFlag(ComicCapabilities.RandomAccess));
        Assert.True(rar.Capabilities.HasFlag(ComicCapabilities.RandomAccess));
        Assert.True(sevenZip.Capabilities.HasFlag(ComicCapabilities.ReadContent));
    }

    [Fact]
    public async Task Reads_a_rar_named_as_a_cbz()
    {
        var rar = _fixture.ExtractAsset("sample.cbr");
        var misnamed = _fixture.PathFor("misnamed.cbz");
        File.Move(rar, misnamed);

        await using var comic = await ComicBook.OpenAsync(misnamed);

        Assert.Equal(ComicContainerFormat.Cbr, comic.Format);
        Assert.Equal(3, comic.Pages.Count);
    }

    private string CreateComic(string kind)
    {
        var entries = new (string, byte[])[]
        {
            ("1.jpg", TestImages.Jpeg(1)),
            ("2.jpg", TestImages.Jpeg(2)),
            ("10.jpg", TestImages.Jpeg(10)),
            ("ComicInfo.xml", System.Text.Encoding.UTF8.GetBytes(ComicInfo)),
        };

        return kind switch
        {
            "cbz" => _fixture.CreateZip("comic.cbz", entries),
            "cbt" => _fixture.CreateTar("comic.cbt", entries),
            _ => _fixture.ExtractAsset($"sample.{kind}"),
        };
    }

    public void Dispose() => _fixture.Dispose();
}
