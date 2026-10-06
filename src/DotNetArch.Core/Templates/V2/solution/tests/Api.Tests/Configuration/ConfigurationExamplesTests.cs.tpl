using System.Text.Json;
using {{App}}.Api.Configuration;

namespace {{App}}.Api.Tests.Configuration;

/// <summary>Keeps the checked-in examples honest: they are the documented contract of the service's configuration.</summary>
[Trait("Category", "Configuration")]
public class ConfigurationExamplesTests
{
    private static readonly string ApiFolder = Path.Combine(SolutionRoot(), "src", "{{App}}.Api");

    private static string SolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "global.json")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException("Solution root (global.json) not found.");
    }

    private static SortedSet<string> Keys(string file)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(ApiFolder, file)));
        var keys = new SortedSet<string>(StringComparer.Ordinal);
        Walk(document.RootElement, string.Empty, keys);
        return keys;
    }

    private static void Walk(JsonElement element, string prefix, ISet<string> keys)
    {
        foreach (var property in element.EnumerateObject())
        {
            var path = prefix.Length == 0 ? property.Name : $"{prefix}:{property.Name}";
            if (property.Value.ValueKind == JsonValueKind.Object)
                Walk(property.Value, path, keys);
            else
                keys.Add(path);
        }
    }

    [Fact]
    public void Env_example_lists_every_secret_key_of_the_contract()
    {
        var example = File.ReadAllText(Path.Combine(ApiFolder, ".env.example"));

        foreach (var key in ConfigurationContract.SecretKeys)
            Assert.Contains($"{key}=", example);
    }

    [Fact]
    public void Appsettings_example_mirrors_appsettings() =>
        Assert.Equal(Keys("appsettings.json"), Keys("appsettings.example.json"));

    [Fact]
    public void Appsettings_never_contains_secrets()
    {
        var forbidden = new[] { "password", "secret", "connectionstring", "apikey", "token" };

        var offenders = Keys("appsettings.json")
            .Where(key => forbidden.Any(word => key.Contains(word, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Secret_keys_follow_the_upper_case_convention() =>
        Assert.All(ConfigurationContract.SecretKeys, key => Assert.Matches("^[A-Z][A-Z0-9]*(_[A-Z0-9]+)*$", key));
}
