namespace ComiX.Internal;

/// <summary>
/// Turns archive entry names into destination paths that cannot escape the destination directory.
/// </summary>
/// <remarks>
/// Archive entry names are untrusted input: they may be absolute or rooted, contain <c>..</c>
/// segments, use either separator, or contain characters invalid on the host file system. Names are
/// sanitised segment by segment and the resolved path is then verified to be contained within the
/// destination root.
/// </remarks>
internal static class PathSafety
{
    /// <summary>
    /// Resolves <paramref name="entryName"/> to an absolute path under <paramref name="destinationRoot"/>.
    /// </summary>
    /// <exception cref="ComicArchiveException">
    /// The entry name cannot be represented as a path inside the destination directory.
    /// </exception>
    public static string ResolveDestination(string destinationRoot, string entryName, bool flatten)
    {
        var root = Path.GetFullPath(destinationRoot);
        var relative = flatten ? SanitiseSegment(GetFileName(entryName)) : SanitiseRelativePath(entryName);

        if (relative.Length == 0)
        {
            throw new ComicArchiveException(
                $"Entry '{entryName}' does not have a usable file name.",
                entryName);
        }

        var candidate = Path.GetFullPath(Path.Combine(root, relative));
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        if (!candidate.StartsWith(rootWithSeparator, PathComparison))
        {
            throw new ComicArchiveException(
                $"Entry '{entryName}' resolves outside the destination directory and was rejected.",
                entryName);
        }

        return candidate;
    }

    /// <summary>
    /// Whether an entry name would escape the archive root, independently of any destination
    /// directory. Used by validation to report such entries without extracting them.
    /// </summary>
    public static bool IsTraversal(string entryName)
    {
        foreach (var segment in Split(entryName))
        {
            if (segment is "..")
            {
                return true;
            }
        }

        return Path.IsPathRooted(entryName) || entryName.StartsWith('/') || entryName.StartsWith('\\');
    }

    public static string GetFileName(string entryName)
    {
        var normalised = entryName.Replace('\\', '/');
        var index = normalised.LastIndexOf('/');
        return index < 0 ? normalised : normalised[(index + 1)..];
    }

    public static string GetDirectoryName(string entryName)
    {
        var normalised = entryName.Replace('\\', '/');
        var index = normalised.LastIndexOf('/');
        return index < 0 ? string.Empty : normalised[..index];
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    private static string SanitiseRelativePath(string entryName)
    {
        var segments = new List<string>();
        foreach (var segment in Split(entryName))
        {
            if (segment is "." or "..")
            {
                continue;
            }

            var safe = SanitiseSegment(segment);
            if (safe.Length > 0)
            {
                segments.Add(safe);
            }
        }

        return segments.Count == 0 ? string.Empty : Path.Combine([.. segments]);
    }

    private static string[] Split(string entryName) =>
        entryName.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);

    private static string SanitiseSegment(string segment)
    {
        // Drive-relative names such as "C:file" and reserved characters are sanitised rather than
        // rejected, so that one malformed entry does not fail the entire extraction.
        Span<char> buffer = segment.Length <= 256 ? stackalloc char[segment.Length] : new char[segment.Length];
        var length = 0;
        foreach (var c in segment)
        {
            buffer[length++] = c is ':' or '*' or '?' or '"' or '<' or '>' or '|' || char.IsControl(c) ? '_' : c;
        }

        return new string(buffer[..length]).Trim();
    }
}
