using {{App}}.Api.Configuration;

namespace {{App}}.Api.Tests.Configuration;

public sealed class EnvFileTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"envfile-{Guid.NewGuid():N}.env");

    public void Dispose()
    {
        if (File.Exists(_path))
            File.Delete(_path);
    }

    [Fact]
    public void Reads_values_comments_quotes_and_section_separators()
    {
        File.WriteAllText(_path, "# comment\nPLAIN=value\nQUOTED=\"two words\"\nSINGLE='x'\nDatabase__MigrateOnStartup=true\n\nbroken line\n");

        var values = EnvFile.Read(_path);

        Assert.Equal("value", values["PLAIN"]);
        Assert.Equal("two words", values["QUOTED"]);
        Assert.Equal("x", values["SINGLE"]);
        Assert.Equal("true", values["Database:MigrateOnStartup"]);
        Assert.Equal(4, values.Count);
    }

    [Fact]
    public void A_missing_file_yields_no_values() =>
        Assert.Empty(EnvFile.Read(Path.Combine(Path.GetTempPath(), "does-not-exist.env")));
}
