using System.Text;

namespace ComiX;

/// <summary>
/// The outcome of validating a comic archive.
/// </summary>
/// <remarks>
/// Only findings of severity <see cref="ComicValidationSeverity.Error"/> clear <see cref="IsValid"/>.
/// Metadata defects are reported as warnings.
/// </remarks>
public sealed record ComicValidationResult
{
    /// <summary>All findings, in the order they were produced.</summary>
    public required IReadOnlyList<ComicValidationIssue> Issues { get; init; }

    /// <summary>The container format that was validated.</summary>
    public required ComicContainerFormat Format { get; init; }

    /// <summary>The number of pages, or <see langword="null"/> when the archive could not be read.</summary>
    public int? PageCount { get; init; }

    /// <summary>Whether the archive is usable as a comic, which is true when no finding is an error.</summary>
    public bool IsValid => !Issues.Any(issue => issue.Severity is ComicValidationSeverity.Error);

    /// <summary>The findings that prevent the archive from being used.</summary>
    public IEnumerable<ComicValidationIssue> Errors =>
        Issues.Where(issue => issue.Severity is ComicValidationSeverity.Error);

    /// <summary>The findings that report an anomaly without preventing use.</summary>
    public IEnumerable<ComicValidationIssue> Warnings =>
        Issues.Where(issue => issue.Severity is ComicValidationSeverity.Warning);
}

/// <summary>
/// Options for <see cref="ComicValidator"/>.
/// </summary>
public sealed record ComicValidationOptions
{
    /// <summary>The options used when none are supplied.</summary>
    public static ComicValidationOptions Default { get; } = new();

    /// <summary>
    /// Decompresses every page and verifies that its content begins with a signature consistent with
    /// the image format implied by its file extension. Reads the entire archive; the default performs
    /// a structural check of the container index only.
    /// </summary>
    public bool DeepScan { get; init; }

    /// <summary>The password for archives with encrypted entries.</summary>
    public string? Password { get; init; }

    /// <summary>
    /// The encoding assumed for entry names that the archive does not declare as UTF-8. See
    /// <see cref="ComicOpenOptions.EntryNameEncoding"/>.
    /// </summary>
    public Encoding? EntryNameEncoding { get; init; }

    /// <summary>The maximum content read from a single entry during a deep scan, 256 MiB by default.</summary>
    public long MaxEntrySizeInBytes { get; init; } = 256L * 1024 * 1024;
}
