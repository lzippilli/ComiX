using ComiX.Tests.Fixtures;
using Xunit;

namespace ComiX.Tests;

public sealed class ValidationTests : IDisposable
{
    private readonly ComicFixture _fixture = new();

    [Fact]
    public async Task A_well_formed_comic_produces_no_findings()
    {
        var path = _fixture.CreateStandardComic(
            "comic.cbz",
            "<ComicInfo><Series>Batman</Series><PageCount>3</PageCount></ComicInfo>");

        var result = await ComicValidator.ValidateAsync(path);

        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
        Assert.Equal(3, result.PageCount);
    }

    [Fact]
    public async Task An_archive_without_images_is_an_error()
    {
        var path = _fixture.CreateZip("comic.cbz", [("readme.txt", TestImages.NotAnImage())]);

        var result = await ComicValidator.ValidateAsync(path);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, issue => issue.Code is ComicValidationCode.NoPages);
        Assert.Contains(result.Warnings, issue => issue.Code is ComicValidationCode.UnsupportedEntryFormat);
    }

    [Fact]
    public async Task A_file_that_is_not_an_archive_is_an_error()
    {
        var path = _fixture.CreateRaw("comic.cbz", "not an archive"u8.ToArray());

        var result = await ComicValidator.ValidateAsync(path);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, issue => issue.Code is ComicValidationCode.ContainerNotRecognised);
    }

    [Fact]
    public async Task A_misnamed_but_readable_comic_is_valid_with_a_warning()
    {
        var path = _fixture.CreateStandardComic("mislabelled.cbr");

        var result = await ComicValidator.ValidateAsync(path);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, issue => issue.Code is ComicValidationCode.ExtensionMismatch);
    }

    [Fact]
    public async Task Imperfect_metadata_is_a_warning_not_an_error()
    {
        var path = _fixture.CreateStandardComic(
            "comic.cbz",
            "<ComicInfo><Series>Batman</Series><Nonsense>1</Nonsense><PageCount>99</PageCount></ComicInfo>");

        var result = await ComicValidator.ValidateAsync(path);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, issue => issue.Code is ComicValidationCode.UnknownMetadataField);
        Assert.Contains(result.Warnings, issue => issue.Code is ComicValidationCode.MetadataPageCountMismatch);
    }

    [Fact]
    public async Task Unparseable_metadata_is_a_warning_not_an_error()
    {
        var path = _fixture.CreateStandardComic("comic.cbz", "<ComicInfo><Series>Batman</Ser");

        var result = await ComicValidator.ValidateAsync(path);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, issue => issue.Code is ComicValidationCode.MetadataUnreadable);
    }

    [Fact]
    public async Task Entries_that_would_escape_the_destination_are_an_error()
    {
        var path = _fixture.CreateZip("evil.cbz",
        [
            ("1.jpg", TestImages.Jpeg(1)),
            ("../escaped.jpg", TestImages.Jpeg(2)),
        ]);

        var result = await ComicValidator.ValidateAsync(path);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, issue => issue.Code is ComicValidationCode.PathTraversalEntry);
    }

    [Fact]
    public async Task A_deep_scan_notices_content_that_is_not_the_image_it_claims_to_be()
    {
        var path = _fixture.CreateZip("comic.cbz",
        [
            ("1.jpg", TestImages.Jpeg(1)),
            ("2.jpg", TestImages.NotAnImage()),
        ]);

        var shallow = await ComicValidator.ValidateAsync(path);
        var deep = await ComicValidator.ValidateAsync(path, new ComicValidationOptions { DeepScan = true });

        Assert.DoesNotContain(shallow.Issues, issue => issue.Code is ComicValidationCode.ImageContentMismatch);
        var mismatch = Assert.Single(
            deep.Warnings,
            issue => issue.Code is ComicValidationCode.ImageContentMismatch);
        Assert.Equal("2.jpg", mismatch.EntryName);
        Assert.True(deep.IsValid);
    }

    [Fact]
    public async Task A_deep_scan_reports_empty_pages()
    {
        var path = _fixture.CreateZip("comic.cbz", [("1.jpg", [])]);

        var result = await ComicValidator.ValidateAsync(path, new ComicValidationOptions { DeepScan = true });

        Assert.Contains(result.Warnings, issue => issue.Code is ComicValidationCode.EmptyEntry);
    }

    [Fact]
    public async Task A_recognised_but_truncated_container_is_an_error()
    {
        var path = _fixture.CreateRaw("comic.cb7", [0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C, 0x00, 0x04]);

        var result = await ComicValidator.ValidateAsync(path);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, issue => issue.Code is ComicValidationCode.ContainerUnreadable);
        Assert.Equal(ComicContainerFormat.Cb7, result.Format);
    }

    public void Dispose() => _fixture.Dispose();
}
