namespace ComiX;

/// <summary>
/// A container format ComiX can write when exporting a comic.
/// </summary>
/// <remarks>
/// This is deliberately a separate type from <see cref="ComicContainerFormat"/>: ComiX reads four
/// containers but writes one, and a separate type makes "export to CBR" a compile error rather than
/// a runtime failure. RAR compression is proprietary and cannot be produced by a managed library,
/// and there is no practical reason to export a comic as 7-Zip or TAR.
/// </remarks>
public enum ComicExportFormat
{
    /// <summary>A ZIP container written with the <c>.cbz</c> convention.</summary>
    Cbz = 1,
}
