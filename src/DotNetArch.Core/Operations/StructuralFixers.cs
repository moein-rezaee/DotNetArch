using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DotNetArch.Core.Doctor;
using DotNetArch.Core.Hosting;

namespace DotNetArch.Core.Operations;

/// <summary>
/// Opt-in fixers (<c>fix --rules=DA-B03,...</c>): they change project structure rather than add a hygiene file, so they never run by default.
/// None of them edits source code; the resolved package versions and the runtime behaviour stay identical.
/// </summary>
internal static partial class StructuralFixers
{
    public static readonly IReadOnlySet<string> RuleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "DA-B03", "DA-B07", "DA-S04", "DA-S06", "DA-A01", "DA-A02", "DA-A08", "DA-A09", "DA-A10", "DA-A11" };

    public static IReadOnlyList<FixAction> For(string ruleId, string root, RepoContext ctx, bool centralPackages) => ruleId switch
    {
        "DA-B03" => CentralPackages(root, ctx),
        "DA-B07" => StrictWarnings(root),
        "DA-S04" => LayerTests(root, ctx, ProjectLayer.Domain, centralPackages || ctx.Has("Directory.Packages.props"), "DA-S04"),
        "DA-S06" => LayoutV2(root, ctx),
        "DA-A01" => LayoutV3(root, ctx),
        "DA-A02" => LayerMigration.Plan(root, ctx, allowEmpty: false, out _),
        "DA-A08" => TypedClient.Plan(root, ctx, out _),
        "DA-A09" => EtcMove.Plan(ctx),
        "DA-A10" => LayerTree.Plan(ctx),
        "DA-A11" => LayerReferences.Plan(root, ctx, out _),
        _ => Array.Empty<FixAction>(),
    };

    /// <summary>
    /// Moves package versions into Directory.Packages.props. The most common version of a package becomes the central one; a project that
    /// pinned a different version keeps it through <c>VersionOverride</c>, so every project resolves exactly what it resolved before.
    /// </summary>
    private static IReadOnlyList<FixAction> CentralPackages(string root, RepoContext ctx)
    {
        if (ctx.Has("Directory.Packages.props"))
            return Array.Empty<FixAction>();

        var documents = new List<(ProjectInfo Project, XDocument Doc)>();
        var versions = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
        foreach (var project in ctx.Projects)
        {
            var doc = XDocument.Load(Path.Combine(root, project.File), LoadOptions.PreserveWhitespace);
            documents.Add((project, doc));
            foreach (var reference in doc.Descendants("PackageReference"))
            {
                var id = (string?)reference.Attribute("Include");
                var version = (string?)reference.Attribute("Version") ?? reference.Element("Version")?.Value;
                if (id == null || version == null || version.Contains("$(", StringComparison.Ordinal))
                    continue;
                versions.TryAdd(id, new Dictionary<string, int>(StringComparer.Ordinal));
                versions[id][version] = versions[id].GetValueOrDefault(version) + 1;
            }
        }

        if (versions.Count == 0)
            return Array.Empty<FixAction>();

        var central = versions.ToDictionary(v => v.Key, v => v.Value.OrderByDescending(x => x.Value).ThenByDescending(x => x.Key, StringComparer.Ordinal).First().Key, StringComparer.Ordinal);
        var actions = new List<FixAction>();
        var props = new StringBuilder("<Project>\n\n  <PropertyGroup>\n    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>\n  </PropertyGroup>\n\n  <ItemGroup>\n");
        foreach (var (id, version) in central.OrderBy(c => c.Key, StringComparer.OrdinalIgnoreCase))
            props.Append($"    <PackageVersion Include=\"{id}\" Version=\"{version}\" />\n");
        props.Append("  </ItemGroup>\n\n</Project>\n");
        actions.Add(new FixAction(new PlannedChange("Directory.Packages.props", "create", $"central versions for {central.Count} packages (differing pins stay as VersionOverride)", "DA-B03"), props.ToString()));

        foreach (var (project, doc) in documents)
        {
            var changed = false;
            foreach (var reference in doc.Descendants("PackageReference").ToList())
            {
                var id = (string?)reference.Attribute("Include");
                var versionAttribute = reference.Attribute("Version");
                var versionElement = reference.Element("Version");
                var version = versionAttribute?.Value ?? versionElement?.Value;
                if (id == null || version == null || !central.TryGetValue(id, out var wanted))
                    continue;
                versionAttribute?.Remove();
                versionElement?.Remove();
                if (!string.Equals(version, wanted, StringComparison.Ordinal))
                    reference.SetAttributeValue("VersionOverride", version);
                changed = true;
            }

            if (changed)
                actions.Add(new FixAction(new PlannedChange(project.File, "modify", "package versions moved to Directory.Packages.props", "DA-B03"), doc.ToString(SaveOptions.DisableFormatting)));
        }

        return actions;
    }

    /// <summary>Adds the CI/Release rule "warnings are errors" to Directory.Build.props (created when absent).</summary>
    private static IReadOnlyList<FixAction> StrictWarnings(string root)
    {
        const string group = "  <!-- CI and Release builds must be warning-free -->\n  <PropertyGroup Condition=\"'$(CI)' == 'true' or '$(GITHUB_ACTIONS)' == 'true' or '$(Configuration)' == 'Release'\">\n    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>\n  </PropertyGroup>\n";
        var path = Path.Combine(root, "Directory.Build.props");
        if (!File.Exists(path))
            return new[] { new FixAction(new PlannedChange("Directory.Build.props", "create", "warnings are errors in CI and Release", "DA-B07"), "<Project>\n\n" + group + "\n</Project>\n") };

        var text = File.ReadAllText(path);
        var close = text.LastIndexOf("</Project>", StringComparison.Ordinal);
        if (close < 0)
            return Array.Empty<FixAction>();
        return new[] { new FixAction(new PlannedChange("Directory.Build.props", "modify", "warnings are errors in CI and Release", "DA-B07"), text[..close].TrimEnd('\n') + "\n\n" + group + text[close..]) };
    }

    /// <summary>
    /// Adds the missing test project of a layer (<c>&lt;Project&gt;.Tests</c> in the test folder) with an architecture test: the assembly loads and references no project its layer must not know.
    /// Used by DA-S04 (Domain) and by the <c>add_tests</c> operation (any layer). The project is registered in the solution.
    /// </summary>
    internal static IReadOnlyList<FixAction> LayerTests(string root, RepoContext ctx, ProjectLayer layer, bool central, string ruleId)
    {
        var target = ctx.OfLayer(layer).FirstOrDefault();
        if (target == null || ctx.OfLayer(layer, tests: true).Any())
            return Array.Empty<FixAction>();

        var sibling = ctx.Projects.FirstOrDefault(p => p.IsTest);
        var siblingDoc = sibling == null ? null : XDocument.Load(Path.Combine(root, sibling.File));
        var framework = siblingDoc?.Descendants("TargetFramework").FirstOrDefault()?.Value ?? XDocument.Load(Path.Combine(root, target.File)).Descendants("TargetFramework").FirstOrDefault()?.Value ?? "net8.0";
        var testPackages = new[] { "Microsoft.NET.Test.Sdk", "xunit", "xunit.runner.visualstudio" };
        var packageLines = new StringBuilder();
        foreach (var name in testPackages)
        {
            var reference = siblingDoc?.Descendants("PackageReference").FirstOrDefault(r => (string?)r.Attribute("Include") == name);
            var version = (string?)reference?.Attribute("Version");
            packageLines.Append(version == null || central
                ? $"    <PackageReference Include=\"{name}\" />\n"
                : $"    <PackageReference Include=\"{name}\" Version=\"{version}\" />\n");
        }

        var testRoot = ctx.DirectoryExists("test") || !ctx.DirectoryExists("tests") ? "test" : "tests";
        var sourceFolder = target.Dir.Contains('/') ? target.Dir[..target.Dir.LastIndexOf('/')] : string.Empty;
        var prefix = sourceFolder.Length == 0 ? string.Empty : sourceFolder.Contains('/') ? sourceFolder[..sourceFolder.LastIndexOf('/')] + "/" : string.Empty;
        var testName = target.Name + ".Tests";
        var dir = sibling != null && sibling.Dir.Contains('/') ? sibling.Dir[..sibling.Dir.LastIndexOf('/')] + "/" + testName : (sourceFolder.Length == 0 ? testName : $"{prefix}{testRoot}/{testName}");
        var relative = Path.GetRelativePath(dir, target.File).Replace('/', '\\');
        var csproj = $"<Project Sdk=\"Microsoft.NET.Sdk\">\n\n  <PropertyGroup>\n    <TargetFramework>{framework}</TargetFramework>\n    <IsPackable>false</IsPackable>\n    <IsTestProject>true</IsTestProject>\n    <Nullable>enable</Nullable>\n    <ImplicitUsings>enable</ImplicitUsings>\n  </PropertyGroup>\n\n  <ItemGroup>\n{packageLines}  </ItemGroup>\n\n  <ItemGroup>\n    <ProjectReference Include=\"{relative}\" />\n  </ItemGroup>\n\n</Project>\n";
        var forbidden = ctx.Source.Where(p => p.Name != target.Name && p.Layer != ProjectLayer.Other && !LayerReferences.Allowed(layer, p.Layer)).Select(p => $"\"{p.Name}\"").ToList();
        var label = target.Name[(target.Name.IndexOf('.') + 1)..];
        var testClass = label.Replace(".", string.Empty) + "LayerTests";
        var test = $"using System.Reflection;\nusing Xunit;\n\nnamespace {testName};\n\n/// <summary>Architecture rule: dependencies point inwards, so {label} must not know a layer outside what it may reference.</summary>\npublic sealed class {testClass}\n{{\n    private static readonly string[] Forbidden = {{ {string.Join(", ", forbidden)} }};\n\n    [Fact]\n    public void Layer_does_not_reference_a_project_it_must_not_know()\n    {{\n        var offenders = Assembly.Load(\"{target.Name}\").GetReferencedAssemblies()\n            .Select(a => a.Name ?? string.Empty)\n            .Where(name => Forbidden.Contains(name, StringComparer.Ordinal))\n            .ToList();\n\n        Assert.Empty(offenders);\n    }}\n\n    [Fact]\n    public void Layer_assembly_is_loadable() =>\n        Assert.NotNull(Assembly.Load(\"{target.Name}\"));\n}}\n";
        var solution = ctx.Files.FirstOrDefault(f => !f.Contains('/') && f.EndsWith(".sln", StringComparison.OrdinalIgnoreCase));
        return new[]
        {
            new FixAction(new PlannedChange($"{dir}/{testName}.csproj", "create", $"test project for {label}", ruleId), csproj, solution == null ? null : () => RegisterInSolution(root, solution, $"{dir}/{testName}.csproj")),
            new FixAction(new PlannedChange($"{dir}/{testClass}.cs", "create", $"architecture test: {label} references no project it must not know", ruleId), test),
        };
    }

    private static void RegisterInSolution(string root, string solution, string project)
    {
        var result = ToolHost.Runner.Run(new ProcessSpec("dotnet", new[] { "sln", solution, "add", project }, root), showProgress: false);
        if (!result.Success)
            throw new InvalidOperationException($"dotnet sln add failed: {result.Output}");
    }

    // ------------------------------------------------------------------------------------------------------------------------------
    // Layout v3 (ABP): tests/ becomes test/. Same rule as the v2 move: folders move, then every path that points at them is rewritten.

    private static IReadOnlyList<FixAction> LayoutV3(string root, RepoContext ctx)
    {
        if (ctx.Layout != "v2" || !ctx.DirectoryExists("tests") || ctx.DirectoryExists("test"))
            return Array.Empty<FixAction>();

        var actions = new List<FixAction> { new(new PlannedChange("test", "move", "moved from tests", "DA-A01"), string.Empty, MoveFrom: "tests") };
        foreach (var file in ctx.Files)
        {
            var isCode = file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
            var isProject = file.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".props", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".targets", StringComparison.OrdinalIgnoreCase);
            var isSolution = file.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase);
            var isState = file.Equals(".net-arch/project.yml", StringComparison.OrdinalIgnoreCase);
            if (!isCode && !isProject && !isSolution && !isState && !IsRewriteTarget(file))
                continue;

            var text = ctx.Read(file);
            if (text.Length == 0)
                continue;
            var rewritten = isState ? Regex.Replace(text, @"(?m)^layout:\s*v2\s*$", "layout: v3", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1))
                : isCode ? RewriteCodeSegment(text)
                : RewriteSegment(text);
            var newPath = file.StartsWith("tests/", StringComparison.Ordinal) ? "test/" + file["tests/".Length..] : file;
            if (rewritten != text || newPath != file)
            {
                if (rewritten != text)
                    actions.Add(new FixAction(new PlannedChange(newPath, "modify", "paths follow tests/ to test/", "DA-A01"), rewritten));
            }
        }

        return actions;
    }

    /// <summary>A top-level <c>tests</c> path segment becomes <c>test</c>; a segment inside another path (<c>docs/tests/</c>) is left alone.</summary>
    private static string RewriteSegment(string text) =>
        Regex.Replace(text, @"(?<![\w./\\-])(?<dot>(?:\.\.?[/\\])*)tests(?=[/\\])", "${dot}test", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));

    private static string RewriteCodeSegment(string text)
    {
        if (!text.Contains("tests", StringComparison.Ordinal))
            return text;
        text = Regex.Replace(text, @"(Path\.Combine\([^;\n]*?)""tests""(?=\s*[,)])", "$1\"test\"", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
        return Regex.Replace(text, @"(?<=""[^""\n]*?)(?<![\w./\\-])(?<dot>(?:\.\.?/)*)tests(?=/)", "${dot}test", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
    }

    // ------------------------------------------------------------------------------------------------------------------------------
    // Layout v2: projects under src/, test projects under tests/. Moves folders, then rewrites every path that points at them.

    private static readonly string[] RewriteGlobs =
    {
        "Dockerfile*", "docker-compose*.y*ml", "compose*.y*ml", ".gitlab-ci.yml", "azure-pipelines.yml", "bitbucket-pipelines.yml",
        ".gitlab/", ".github/", ".gitea/", ".corevia/", "scripts/", "README", "AGENTS.md", "docs/specs/", "docs/INDEX",
    };

    private static readonly string[] HistoryMarkers = { "changelog", "docs/evidence/", "docs/roadmaps/", "docs/decisions/" };

    private static IReadOnlyList<FixAction> LayoutV2(string root, RepoContext ctx)
    {
        var moved = ctx.Projects.Where(p => p.Dir.Length > 0 && !p.Dir.Contains('/')).ToDictionary(p => p.Dir, p => (p.IsTest ? "tests/" : "src/") + p.Dir, StringComparer.Ordinal);
        if (moved.Count == 0)
            return Array.Empty<FixAction>();

        string Remap(string relative)
        {
            var normalized = relative.Replace('\\', '/');
            var first = normalized.Split('/')[0];
            return moved.TryGetValue(first, out var target) ? target + normalized[first.Length..] : normalized;
        }

        var actions = new List<FixAction>();
        foreach (var (oldDir, newDir) in moved.OrderBy(m => m.Key, StringComparer.Ordinal))
            actions.Add(new FixAction(new PlannedChange(newDir, "move", $"moved from {oldDir}", "DA-S06"), string.Empty, MoveFrom: oldDir));

        foreach (var file in ctx.Files)
        {
            var oldPath = file;
            var newPath = Remap(file);
            var isCode = file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
            var isProject = file.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) && moved.ContainsKey(file.Split('/')[0]);
            var isSolution = file.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) && !file.Contains('/');
            var textFile = IsRewriteTarget(file);
            if (!isProject && !isSolution && !textFile && !isCode)
                continue;

            var text = ctx.Read(oldPath);
            if (text.Length == 0)
                continue;
            var rewritten = isCode ? RewriteCodePaths(text, moved)
                : isProject ? RewriteProjectPaths(text, oldPath, newPath, moved)
                : isSolution ? RewriteSolution(text, moved)
                : RewriteText(text, moved, file.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".yml", StringComparison.OrdinalIgnoreCase));
            if (rewritten != text)
                actions.Add(new FixAction(new PlannedChange(newPath, "modify", "paths follow the moved projects", "DA-S06"), rewritten));
        }

        return actions;
    }

    internal static bool IsTextRewriteTarget(string file) => IsRewriteTarget(file);

    private static bool IsRewriteTarget(string file)
    {
        var lower = file.ToLowerInvariant();
        if (HistoryMarkers.Any(h => lower.Contains(h, StringComparison.Ordinal)))
            return false;
        var name = Path.GetFileName(lower);
        return RewriteGlobs.Any(g => g.EndsWith('/') ? lower.StartsWith(g, StringComparison.Ordinal)
            : g.Contains('*') ? GlobMatch(name, g.ToLowerInvariant())
            : name.StartsWith(g.ToLowerInvariant(), StringComparison.Ordinal) && !lower.Contains('/')
              || name.Equals(g.ToLowerInvariant(), StringComparison.Ordinal));
    }

    private static bool GlobMatch(string name, string glob) =>
        Regex.IsMatch(name, "^" + Regex.Escape(glob).Replace("\\*", ".*", StringComparison.Ordinal) + "$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    private static string RewriteText(string text, IReadOnlyDictionary<string, string> moved, bool yaml)
    {
        foreach (var name in moved.Keys.OrderByDescending(k => k.Length))
        {
            var prefix = moved[name][..moved[name].IndexOf('/', StringComparison.Ordinal)];
            text = Regex.Replace(text, $@"(?<![\w./\\-])(?<dot>\./)?{Regex.Escape(name)}(?=[/\\])", $"${{dot}}{prefix}/{name}", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
            if (yaml)
            {
                // a whole value such as `path: Shop.Api.Tests` names a project folder too
                text = Regex.Replace(text, $@"(?m)(?<key>^\s*-?\s*(?:path|dir|directory|folder|project|working_directory):\s*[""']?)(?<dot>\./)?{Regex.Escape(name)}(?<end>[""']?\s*$)", $"${{key}}${{dot}}{prefix}/{name}${{end}}", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
            }
        }

        return text;
    }

    /// <summary>
    /// Only path literals that name a moved project folder are touched (<c>Path.Combine(root, "Shop.Api", ...)</c> and <c>"Shop.Api/..."</c>); namespaces and
    /// type names are not path literals and stay as they are.
    /// </summary>
    private static string RewriteCodePaths(string text, IReadOnlyDictionary<string, string> moved)
    {
        if (!text.Contains("Path.Combine", StringComparison.Ordinal) && !text.Contains('/') && !text.Contains("\\\\", StringComparison.Ordinal))
            return text;
        foreach (var name in moved.Keys.OrderByDescending(k => k.Length))
        {
            var prefix = moved[name][..moved[name].IndexOf('/', StringComparison.Ordinal)];
            text = Regex.Replace(text, $@"(Path\.Combine\([^;\n]*?,\s*)""{Regex.Escape(name)}""(?=\s*[,)])", $"$1\"{prefix}\", \"{name}\"", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
        }

        return text.Contains('"', StringComparison.Ordinal) ? RewriteQuotedPaths(text, moved) : text;
    }

    private static string RewriteQuotedPaths(string text, IReadOnlyDictionary<string, string> moved)
    {
        foreach (var name in moved.Keys.OrderByDescending(k => k.Length))
        {
            var prefix = moved[name][..moved[name].IndexOf('/', StringComparison.Ordinal)];
            text = Regex.Replace(text, $@"(?<=""[^""\n]*?)(?<![\w./\\-])(?<dot>\./)?{Regex.Escape(name)}(?=/)", $"${{dot}}{prefix}/{name}", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
        }

        return text;
    }

    private static string RewriteSolution(string text, IReadOnlyDictionary<string, string> moved) =>
        Regex.Replace(text, "(?<=, \")([^\"]+\\.csproj)(?=\")", match =>
        {
            var value = match.Value;
            var separator = value.Contains('\\') ? '\\' : '/';
            var normalized = value.Replace('\\', '/');
            var first = normalized.Split('/')[0];
            return moved.TryGetValue(first, out var target) ? (target + normalized[first.Length..]).Replace('/', separator) : value;
        }, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));

    /// <summary>Re-bases every path-like attribute of a project file: the target is resolved from the old location, mapped to its new place, and expressed relative to the new location.</summary>
    private static string RewriteProjectPaths(string text, string oldFile, string newFile, IReadOnlyDictionary<string, string> moved)
    {
        var oldDir = Path.GetDirectoryName(oldFile)?.Replace('\\', '/') ?? string.Empty;
        var newDir = Path.GetDirectoryName(newFile)?.Replace('\\', '/') ?? string.Empty;
        return Regex.Replace(text, "(?<attribute>\\b(?:Include|Update|Remove)\\s*=\\s*\")(?<value>[^\"]+)(?<close>\")", match =>
        {
            var value = match.Groups["value"].Value;
            if (value.Contains("$(", StringComparison.Ordinal) || (!value.Contains('/') && !value.Contains('\\')))
                return match.Value;
            var separator = value.Contains('\\') ? '\\' : '/';
            var absoluteOld = NormalizeLexically(oldDir + "/" + value.Replace('\\', '/'));
            var absoluteNew = Remap(absoluteOld, moved);
            var relative = RelativeLexically(newDir, absoluteNew).Replace('/', separator);
            return match.Groups["attribute"].Value + relative + match.Groups["close"].Value;
        }, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
    }

    private static string Remap(string relative, IReadOnlyDictionary<string, string> moved)
    {
        var first = relative.Split('/')[0];
        return moved.TryGetValue(first, out var target) ? target + relative[first.Length..] : relative;
    }

    private static string NormalizeLexically(string path)
    {
        var parts = new List<string>();
        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
                continue;
            if (segment == ".." && parts.Count > 0 && parts[^1] != "..")
                parts.RemoveAt(parts.Count - 1);
            else
                parts.Add(segment);
        }

        return string.Join('/', parts);
    }

    private static string RelativeLexically(string fromDirectory, string target)
    {
        var from = fromDirectory.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var to = target.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var common = 0;
        while (common < from.Length && common < to.Length - 1 && from[common] == to[common])
            common++;
        return string.Join('/', Enumerable.Repeat("..", from.Length - common).Concat(to.Skip(common)));
    }
}
