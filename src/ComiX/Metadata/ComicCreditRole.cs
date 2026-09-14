namespace ComiX;

/// <summary>
/// The role a person is credited for, such as writer or colorist.
/// </summary>
/// <remarks>
/// Metadata standards use different names for equivalent roles and define roles the others lack.
/// Well-known roles compare equal regardless of the spelling used by the source, and unrecognised
/// roles are retained through <see cref="Custom(string)"/>. Comparison is case-insensitive; the
/// original spelling is preserved in <see cref="Name"/>.
/// </remarks>
public readonly struct ComicCreditRole : IEquatable<ComicCreditRole>
{
    private readonly string? _name;

    private ComicCreditRole(string name) => _name = name;

    /// <summary>The role's name, as written in the source when the role is not a well-known one.</summary>
    public string Name => _name ?? "Unknown";

    /// <summary>Whether this is one of the roles ComiX recognises across standards.</summary>
    public bool IsKnown => _name is not null && KnownRoles.Contains(_name);

    /// <summary>An unspecified role. This is the value of a default-constructed instance.</summary>
    public static ComicCreditRole Unknown => default;

    /// <summary>The writer of the script.</summary>
    public static ComicCreditRole Writer { get; } = new("Writer");

    /// <summary>An artist whose specific contribution is not distinguished.</summary>
    public static ComicCreditRole Artist { get; } = new("Artist");

    /// <summary>The penciller.</summary>
    public static ComicCreditRole Penciller { get; } = new("Penciller");

    /// <summary>The inker.</summary>
    public static ComicCreditRole Inker { get; } = new("Inker");

    /// <summary>The colorist.</summary>
    public static ComicCreditRole Colorist { get; } = new("Colorist");

    /// <summary>The letterer.</summary>
    public static ComicCreditRole Letterer { get; } = new("Letterer");

    /// <summary>The cover artist.</summary>
    public static ComicCreditRole CoverArtist { get; } = new("CoverArtist");

    /// <summary>The editor.</summary>
    public static ComicCreditRole Editor { get; } = new("Editor");

    /// <summary>The translator.</summary>
    public static ComicCreditRole Translator { get; } = new("Translator");

    /// <summary>
    /// Creates a role from an unrecognised name, preserved as written in the source.
    /// </summary>
    /// <param name="name">The role name from the source metadata.</param>
    /// <returns>A role carrying <paramref name="name"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or whitespace.</exception>
    public static ComicCreditRole Custom(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new ComicCreditRole(name.Trim());
    }

    /// <summary>
    /// Maps a role name from a metadata source onto a well-known role, falling back to
    /// <see cref="Custom(string)"/> when the name is not recognised.
    /// </summary>
    /// <remarks>
    /// Matching ignores case, spaces, hyphens and underscores, and resolves documented synonyms such
    /// as <c>Script</c> and <c>Story</c> to <see cref="Writer"/>, or <c>Colours</c> to
    /// <see cref="Colorist"/>.
    /// </remarks>
    /// <param name="name">The role name from the source metadata.</param>
    /// <returns>The matching well-known role, or a custom role carrying <paramref name="name"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or whitespace.</exception>
    public static ComicCreditRole Parse(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Aliases.TryGetValue(Normalise(name), out var role) ? role : Custom(name);
    }

    /// <inheritdoc />
    public bool Equals(ComicCreditRole other) =>
        string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ComicCreditRole other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Name);

    /// <summary>Compares two roles, ignoring case.</summary>
    public static bool operator ==(ComicCreditRole left, ComicCreditRole right) => left.Equals(right);

    /// <summary>Compares two roles, ignoring case.</summary>
    public static bool operator !=(ComicCreditRole left, ComicCreditRole right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => Name;

    private static string Normalise(string name)
    {
        Span<char> buffer = name.Length <= 64 ? stackalloc char[name.Length] : new char[name.Length];
        var length = 0;
        foreach (var c in name)
        {
            if (c is ' ' or '-' or '_' or '.')
            {
                continue;
            }

            buffer[length++] = char.ToUpperInvariant(c);
        }

        return new string(buffer[..length]);
    }

    private static readonly HashSet<string> KnownRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Writer", "Artist", "Penciller", "Inker", "Colorist", "Letterer", "CoverArtist", "Editor",
        "Translator",
    };

    private static readonly Dictionary<string, ComicCreditRole> Aliases = new(StringComparer.Ordinal)
    {
        ["WRITER"] = Writer,
        ["SCRIPT"] = Writer,
        ["SCRIPTER"] = Writer,
        ["STORY"] = Writer,
        ["AUTHOR"] = Writer,
        ["ARTIST"] = Artist,
        ["ART"] = Artist,
        ["ILLUSTRATOR"] = Artist,
        ["PENCILLER"] = Penciller,
        ["PENCILER"] = Penciller,
        ["PENCILS"] = Penciller,
        ["PENCIL"] = Penciller,
        ["INKER"] = Inker,
        ["INKS"] = Inker,
        ["INK"] = Inker,
        ["COLORIST"] = Colorist,
        ["COLOURIST"] = Colorist,
        ["COLORS"] = Colorist,
        ["COLOURS"] = Colorist,
        ["COLORING"] = Colorist,
        ["LETTERER"] = Letterer,
        ["LETTERS"] = Letterer,
        ["LETTERING"] = Letterer,
        ["COVERARTIST"] = CoverArtist,
        ["COVERART"] = CoverArtist,
        ["COVER"] = CoverArtist,
        ["EDITOR"] = Editor,
        ["EDITS"] = Editor,
        ["TRANSLATOR"] = Translator,
        ["TRANSLATION"] = Translator,
        ["TRANSLATEDBY"] = Translator,
    };
}
