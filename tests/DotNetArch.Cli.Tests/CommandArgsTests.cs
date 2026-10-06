using DotNetArch.Cli.Commands;
using Xunit;

namespace DotNetArch.Cli.Tests;

public class CommandArgsTests
{
    [Fact]
    public void Parses_options_flags_and_positionals()
    {
        var args = CommandArgs.Parse(new[] { "new", "solution", "Acme", "--output=/tmp/x", "--no-database", "--style=fast" }, 2);
        Assert.Equal(new[] { "Acme" }, args.Positionals);
        Assert.Equal("/tmp/x", args.Get("output"));
        Assert.Equal("fast", args.Get("style"));
        Assert.True(args.Has("no-database"));
        Assert.False(args.Has("docker"));
        Assert.Null(args.Get("entity"));
    }

    [Fact]
    public void Values_may_contain_equals_signs() =>
        Assert.Equal("a=b", CommandArgs.Parse(new[] { "--x=a=b" }, 0).Get("x"));

    [Theory]
    [InlineData("new", "crud", true)]
    [InlineData("NEW", "CRUD", true)]
    [InlineData("new", "enum", false)]
    public void CommandMatch_is_case_insensitive(string a, string b, bool expected) =>
        Assert.Equal(expected, new NewCrudCommand().Matches(new[] { a, b }));
}
