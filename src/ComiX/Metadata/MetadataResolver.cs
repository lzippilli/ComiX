using ComiX.Archives;
using ComiX.Internal;

namespace ComiX.Metadata;

/// <summary>
/// Finds every metadata source in an archive and decides which one supplies the canonical values.
/// </summary>
/// <remarks>
/// Precedence is <c>ComicInfo.xml</c>, <c>MetronInfo.xml</c>, CoMet, then ComicBookInfo. The first
/// source that parses supplies all canonical values; fields are not merged across standards.
/// Remaining sources are listed in <see cref="ComicMetadata.Sources"/>. Precedence is defined only
/// here, so it can become a configurable policy without changes to <see cref="ComicBook"/>.
/// </remarks>
internal static class MetadataResolver
{
    private const string ArchiveCommentLocation = "(archive comment)";

    /// <summary>
    /// Reads the archive's metadata. Parsing failures are reported rather than thrown, and resolution
    /// continues with the next source in precedence order.
    /// </summary>
    public static async Task<ResolvedMetadata> ResolveAsync(
        IComicArchive archive,
        ComicContentReader reader,
        CancellationToken cancellationToken)
    {
        var candidates = FindCandidates(archive);
        if (candidates.Count == 0)
        {
            return ResolvedMetadata.None;
        }

        var failures = new List<ComicMetadataFailure>();
        var sources = new List<ComicMetadataSource>();
        ComicMetadataDocument? winner = null;

        foreach (var candidate in candidates)
        {
            if (winner is not null)
            {
                // Already resolved: the remaining sources are recorded, not read.
                sources.Add(new ComicMetadataSource(candidate.Standard, candidate.Location, IsPrimary: false));
                continue;
            }

            try
            {
                winner = await ReadAsync(candidate, archive, reader, cancellationToken).ConfigureAwait(false);
                sources.Add(new ComicMetadataSource(candidate.Standard, candidate.Location, IsPrimary: true));
            }
            catch (ComiXException ex)
            {
                failures.Add(new ComicMetadataFailure(candidate.Standard, candidate.Location, ex.Message));
                sources.Add(new ComicMetadataSource(candidate.Standard, candidate.Location, IsPrimary: false));
            }
        }

        if (winner is null)
        {
            // Every source was unreadable: the archive still declares them, so they are reported.
            return new ResolvedMetadata(
                new ComicMetadataDocument(ComicMetadata.Empty with { Sources = sources }, [], []),
                failures);
        }

        return new ResolvedMetadata(
            winner with { Metadata = winner.Metadata with { Sources = sources } },
            failures);
    }

    /// <summary>
    /// The metadata sources present in the archive, in precedence order. Finding a source does not
    /// mean it can be read.
    /// </summary>
    public static List<MetadataCandidate> FindCandidates(IComicArchive archive)
    {
        var candidates = new List<MetadataCandidate>(4);

        if (ArchiveContent.FindComicInfo(archive.Entries) is { } comicInfo)
        {
            candidates.Add(new MetadataCandidate(ComicMetadataStandard.ComicInfo, comicInfo.Key, comicInfo));
        }

        if (ArchiveContent.FindMetronInfo(archive.Entries) is { } metronInfo)
        {
            candidates.Add(new MetadataCandidate(ComicMetadataStandard.MetronInfo, metronInfo.Key, metronInfo));
        }

        if (ArchiveContent.FindCoMet(archive.Entries) is { } coMet)
        {
            candidates.Add(new MetadataCandidate(ComicMetadataStandard.CoMet, coMet.Key, coMet));
        }

        if (ComicBookInfoReader.LooksLikeComicBookInfo(archive.Comment))
        {
            candidates.Add(new MetadataCandidate(
                ComicMetadataStandard.ComicBookInfo,
                ArchiveCommentLocation,
                Entry: null));
        }

        return candidates;
    }

    private static async Task<ComicMetadataDocument> ReadAsync(
        MetadataCandidate candidate,
        IComicArchive archive,
        ComicContentReader reader,
        CancellationToken cancellationToken)
    {
        if (candidate.Standard is ComicMetadataStandard.ComicBookInfo)
        {
            return ComicBookInfoReader.Read(archive.Comment!, candidate.Location);
        }

        var content = await reader.OpenAsync(candidate.Entry!, cancellationToken).ConfigureAwait(false);
        await using (content.ConfigureAwait(false))
        {
            return candidate.Standard switch
            {
                ComicMetadataStandard.ComicInfo => ComicInfoReader.Read(content, candidate.Location),
                ComicMetadataStandard.MetronInfo => MetronInfoReader.Read(content, candidate.Location),
                ComicMetadataStandard.CoMet => CoMetReader.Read(content, candidate.Location),
                _ => throw new ComicMetadataException($"No reader for {candidate.Standard}."),
            };
        }
    }

    /// <summary>A metadata source found in an archive, before any attempt to read it.</summary>
    public sealed record MetadataCandidate(
        ComicMetadataStandard Standard,
        string Location,
        ComicArchiveEntry? Entry);
}
