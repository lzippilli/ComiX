using ComiX.Tests.Fixtures;
using Xunit;

namespace ComiX.Tests;

public sealed class DetectionTests : IDisposable
{
    private readonly ComicFixture _fixture = new();

    [Fact]
    public async Task Detects_a_cbz_and_counts_its_pages()
    {
        var path = _fixture.CreateStandardComic();

        var result = await ComicDetector.DetectAsync(path);

        Assert.Equal(ComicContainerFormat.Cbz, result.Format);
        Assert.True(result.IsSupported);
        Assert.True(result.ExtensionMatchesFormat);
        Assert.Equal(3, result.PageCount);
        Assert.False(result.HasMetadata);
        Assert.Equal(ComicMetadataStandard.None, result.MetadataStandard);
    }

    [Fact]
    public async Task Reports_metadata_presence_without_parsing_it()
    {
        var path = _fixture.CreateStandardComic("with-metadata.cbz", "<ComicInfo><Title>X</Title></ComicInfo>");

        var result = await ComicDetector.DetectAsync(path);

        Assert.True(result.HasMetadata);
        Assert.Equal(ComicMetadataStandard.ComicInfo, result.MetadataStandard);
    }

    [Fact]
    public async Task Header_only_detection_identifies_the_container_without_reading_its_index()
    {
        var path = _fixture.CreateStandardComic("with-metadata.cbz", "<ComicInfo><Title>X</Title></ComicInfo>");

        var result = await ComicDetector.DetectAsync(
            path,
            new ComicDetectionOptions { Depth = ComicDetectionDepth.Header });

        Assert.Equal(ComicContainerFormat.Cbz, result.Format);
        Assert.True(result.IsSupported);
        Assert.Null(result.PageCount);
        Assert.False(result.HasMetadata);
        Assert.Equal(ComicMetadataStandard.None, result.MetadataStandard);
    }

    [Fact]
    public async Task Trusts_content_over_extension()
    {
        // A ZIP archive named .cbr: extremely common, and the reason extensions are only a hint.
        var path = _fixture.CreateStandardComic("mislabelled.cbr");

        var result = await ComicDetector.DetectAsync(path);

        Assert.Equal(ComicContainerFormat.Cbz, result.Format);
        Assert.False(result.ExtensionMatchesFormat);
    }

    [Fact]
    public async Task Detects_a_tar_container()
    {
        var path = _fixture.CreateTar("comic.cbt", [("1.jpg", TestImages.Jpeg(1))]);

        var result = await ComicDetector.DetectAsync(path);

        Assert.Equal(ComicContainerFormat.Cbt, result.Format);
        Assert.True(result.ExtensionMatchesFormat);
        Assert.True(result.IsSupported);
        Assert.Equal(1, result.PageCount);
    }

    [Theory]
    [InlineData(ComicContainerFormat.Cbr, new byte[] { 0x52, 0x61, 0x72, 0x21, 0x1A, 0x07, 0x01, 0x00 })]
    [InlineData(ComicContainerFormat.Cb7, new byte[] { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C, 0x00, 0x04 })]
    public async Task Identifies_a_truncated_archive_without_failing(ComicContainerFormat expected, byte[] header)
    {
        // Only the signature is present: the format is knowable, its content is not.
        var path = _fixture.CreateRaw("stub.bin", header);

        var result = await ComicDetector.DetectAsync(path);

        Assert.Equal(expected, result.Format);
        Assert.True(result.IsSupported);
        Assert.Null(result.PageCount);
    }

    [Fact]
    public async Task Reports_unknown_for_a_file_that_is_not_an_archive()
    {
        var path = _fixture.CreateRaw("notes.txt", "hello, this is not a comic"u8.ToArray());

        var result = await ComicDetector.DetectAsync(path);

        Assert.Equal(ComicContainerFormat.Unknown, result.Format);
        Assert.False(result.IsSupported);
    }

    [Fact]
    public async Task Reports_the_format_of_a_corrupt_archive_without_failing()
    {
        // A valid ZIP header followed by garbage: recognisable, unreadable.
        var path = _fixture.CreateRaw("corrupt.cbz", [0x50, 0x4B, 0x03, 0x04, 0xFF, 0xFF, 0xFF, 0xFF, 0x00]);

        var result = await ComicDetector.DetectAsync(path);

        Assert.Equal(ComicContainerFormat.Cbz, result.Format);
        Assert.Null(result.PageCount);
    }

    [Fact]
    public async Task Detects_from_a_stream_without_disposing_it()
    {
        var path = _fixture.CreateStandardComic();
        await using var stream = File.OpenRead(path);

        var result = await ComicDetector.DetectAsync(stream);

        Assert.Equal(ComicContainerFormat.Cbz, result.Format);
        Assert.Equal(3, result.PageCount);
        Assert.True(stream.CanRead);
    }

    [Fact]
    public async Task Rejects_a_missing_file()
    {
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => ComicDetector.DetectAsync(_fixture.PathFor("nope.cbz")));
    }

    public void Dispose() => _fixture.Dispose();
}
