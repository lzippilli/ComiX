using Xunit;

namespace ComiX.Tests;

public sealed class CreditRoleTests
{
    [Theory]
    [InlineData("Writer")]
    [InlineData("writer")]
    [InlineData("Script")]
    [InlineData("Story")]
    [InlineData("Author")]
    public void Maps_synonyms_onto_the_same_well_known_role(string name)
    {
        Assert.Equal(ComicCreditRole.Writer, ComicCreditRole.Parse(name));
    }

    [Theory]
    [InlineData("Colourist", "Colorist")]
    [InlineData("Pencils", "Penciller")]
    [InlineData("Lettering", "Letterer")]
    [InlineData("cover art", "CoverArtist")]
    [InlineData("Translated By", "Translator")]
    public void Ignores_spelling_spacing_and_case_differences(string name, string expected)
    {
        Assert.Equal(expected, ComicCreditRole.Parse(name).Name);
    }

    [Fact]
    public void Keeps_roles_it_does_not_recognise_instead_of_discarding_them()
    {
        var role = ComicCreditRole.Parse("Character Designer");

        Assert.Equal("Character Designer", role.Name);
        Assert.False(role.IsKnown);
        Assert.Equal(ComicCreditRole.Custom("Character Designer"), role);
    }

    [Fact]
    public void Compares_case_insensitively_so_sources_do_not_split_a_role_in_two()
    {
        Assert.Equal(ComicCreditRole.Custom("character designer"), ComicCreditRole.Custom("Character Designer"));
        Assert.Equal(
            ComicCreditRole.Custom("Writer").GetHashCode(),
            ComicCreditRole.Writer.GetHashCode());
    }

    [Fact]
    public void Distinguishes_different_roles()
    {
        Assert.NotEqual(ComicCreditRole.Writer, ComicCreditRole.Artist);
        Assert.True(ComicCreditRole.Writer != ComicCreditRole.Penciller);
    }

    [Fact]
    public void Has_a_usable_default_value()
    {
        ComicCreditRole role = default;

        Assert.Equal(ComicCreditRole.Unknown, role);
        Assert.Equal("Unknown", role.Name);
        Assert.False(role.IsKnown);
    }
}
