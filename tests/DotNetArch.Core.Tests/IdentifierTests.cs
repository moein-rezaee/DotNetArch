using DotNetArch.Core.Validation;
using Xunit;

namespace DotNetArch.Core.Tests;

public class IdentifierTests
{
    [Theory]
    [InlineData("Acme")]
    [InlineData("Acme.Billing")]
    [InlineData("My_App2")]
    public void SolutionName_accepts_dotted_identifiers(string name) =>
        Assert.True(Identifier.IsValidSolutionName(name));

    [Theory]
    [InlineData("")]
    [InlineData("2Acme")]
    [InlineData("Acme Billing")]
    [InlineData("Acme;rm -rf /")]
    [InlineData("Acme..Core")]
    [InlineData("-n")]
    public void SolutionName_rejects_unsafe_values(string name) =>
        Assert.False(Identifier.IsValidSolutionName(name));

    [Theory]
    [InlineData("Product", true)]
    [InlineData("_x", true)]
    [InlineData("9lives", false)]
    [InlineData("a b", false)]
    [InlineData("a.b", false)]
    public void Identifier_validity(string value, bool expected) =>
        Assert.Equal(expected, Identifier.IsValid(value));

    [Fact]
    public void Sanitize_strips_everything_but_word_characters() =>
        Assert.Equal("Product_1", Identifier.Sanitize("Pro duct;_1!"));

    [Fact]
    public void RequireIfPresent_ignores_blank_and_throws_for_invalid()
    {
        Identifier.RequireIfPresent(null, "entity name");
        Identifier.RequireIfPresent("  ", "entity name");
        Assert.Throws<ArgumentException>(() => Identifier.RequireIfPresent("bad name", "entity name"));
    }

    [Theory]
    [InlineData("bad\npath")]
    [InlineData("bad\0path")]
    [InlineData("")]
    public void RequirePath_rejects_blank_and_control_characters(string path) =>
        Assert.Throws<ArgumentException>(() => Identifier.RequirePath(path, "output path"));
}
