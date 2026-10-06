using System.Text.RegularExpressions;
using DotNetArch.Core.Hosting;
using DotNetArch.Core.Scaffolding.Solution;
using Xunit;

namespace DotNetArch.Core.Tests;

/// <summary>
/// End-to-end regression guard: generates a legacy-layout solution with the real dotnet SDK and compares the tree
/// with the stored golden copy (random ports, GUIDs and absolute paths are normalised).
/// Slow and needs the SDK + network: excluded from the default run, executed by <c>scripts/smoke.sh</c>
/// (<c>dotnet test --filter Category=Integration</c>).
/// </summary>
[Trait("Category", "Integration")]
public class GoldenSolutionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dotnet-arch-golden-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void New_solution_matches_golden_tree()
    {
        Directory.CreateDirectory(_root);
        var output = new BufferedToolOutput();
        using (ToolHost.Use(new HostContext(new NonInteractivePrompter(), output, new DefaultProcessRunner())))
            SolutionGenerator.Generate(new SolutionRequest("Acme", _root, "Acme.API", "controller", ProviderOverride: "None"));

        var expected = ReadTree(Path.Combine(AppContext.BaseDirectory, "Golden", "legacy-controller-none"));
        var actual = ReadTree(Path.Combine(_root, "Acme"));

        Assert.Equal(expected.Keys.OrderBy(k => k), actual.Keys.OrderBy(k => k));
        foreach (var (path, text) in expected)
            Assert.True(text == actual[path], $"Golden mismatch in {path}\n--- expected ---\n{text}\n--- actual ---\n{actual[path]}");
    }

    private static Dictionary<string, string> ReadTree(string root)
    {
        var skip = new[] { "/.git/", "/obj/", "/bin/" };
        var tree = new Dictionary<string, string>();
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (skip.Any(s => ("/" + relative).Contains(s)))
                continue;
            tree[relative] = Normalize(File.ReadAllText(file));
        }
        return tree;
    }

    private static string Normalize(string text)
    {
        text = text.Replace("\r\n", "\n");
        text = Regex.Replace(text, @"\{[0-9A-Fa-f]{8}-[0-9A-Fa-f-]{27}\}", "{GUID}");
        text = Regex.Replace(text, @"(?m)^path: .*$", "path: PATH");
        text = Regex.Replace(text, @"(?m)^port: .*$", "port: PORT");
        text = Regex.Replace(text, @"localhost:\d+", "localhost:PORT");
        text = Regex.Replace(text, @"""sslPort"": \d+", "\"sslPort\": PORT");
        text = Regex.Replace(text, @"(\bEXPOSE |ASPNETCORE_URLS=http://\+:|""|- "")(\d{4,5})\b", "$1PORT");
        text = Regex.Replace(text, @"""(?:PORT|\d+):\d+""", "\"PORT:PORT\"");
        text = Regex.Replace(text, @"env-hash:[0-9A-F]*", "env-hash:X");
        return text;
    }
}
