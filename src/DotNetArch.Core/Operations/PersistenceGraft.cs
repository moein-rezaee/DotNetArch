using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DotNetArch.Core.Config;
using DotNetArch.Core.Scaffolding.V2;
using DotNetArch.Core.Templating;

namespace DotNetArch.Core.Operations;

/// <summary>
/// <c>add_persistence</c>: gives a service that has no database yet the standard persistence skeleton of a generated solution (domain base types,
/// repository and unit-of-work ports, the EF adapter, the context, the design-time factory, the MediatR pipeline and the test support), with the same
/// templates <c>new_solution</c> uses. From then on <c>new_entity</c>, <c>new_action</c> and <c>apply_blueprint</c> work on the service. Files that
/// exist are never overwritten. A service that already has an EF context is refused: its context is the one to use.
/// </summary>
internal static class PersistenceGraft
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);

    private static readonly string[] SkeletonFolders =
    {
        "src/Domain/Common/",
        "src/Application/Abstractions/",
        "src/Application/Common/",
        "src/Application/DependencyInjection.cs",
        "src/Infrastructure/Persistence/",
        "src/Infrastructure/Messaging/",
        "src/Infrastructure/DependencyInjection.cs",
        "tests/Application.Tests/Support/",
        "tests/Application.Tests/Common/",
        "tests/Domain.Tests/Common/",
        "tests/Infrastructure.Tests/Support/",
    };

    public static readonly OperationDefinition Definition = new(
        "add_persistence",
        "Give a service that has no database the standard persistence skeleton (domain base types, repository and unit-of-work ports, EF adapter and context, design-time factory, MediatR pipeline, test support) so new_entity, new_action and apply_blueprint work on it. Existing files are never overwritten; a service with an EF context is refused. Plan first; writes only with apply.",
        OperationKind.Mutating,
        new[]
        {
            new OperationParameter("path", "Repository root of the service.", Required: true, Positional: true, Alias: "output", Default: "@saved"),
            new OperationParameter("database", "Database provider.", Choices: new[] { "SQLite", "SqlServer", "Postgres" }, Default: "Postgres"),
        },
        Run);

    private static OperationResult Run(OperationRequest request)
    {
        var path = Path.GetFullPath(request.Get("path") ?? throw new ArgumentException("Missing required value 'path'."));
        if (!Directory.Exists(path))
            throw new ArgumentException($"Folder not found: {path}");

        var provider = DatabaseProviders.Normalize(request.Get("database") ?? "Postgres");
        return ScaffoldRunner.Run(request, "add_persistence", path, skipMigrations: true, work =>
        {
            var config = ConfigManager.Load(work) ?? ConfigDeriver.Derive(work)
                ?? throw new InvalidOperationException("The folder is not a layout v2/v3 solution with an Api project.");
            config.SolutionPath = work;
            if (TenancyModel.Find(work, out _) is not null)
                throw new InvalidOperationException("The service already has an EF context; use it (new_entity needs the persistence skeleton wired to it).");

            config.DatabaseProvider = provider;
            Apply(work, config, provider);
            // The file records the real folder, not the throw-away copy a plan runs in.
            config.SolutionPath = path;
            ConfigManager.Save(work, config);
        }, normalize: false);
    }

    private static void Apply(string work, SolutionConfig config, string provider)
    {
        var app = config.SolutionName;
        var tfm = string.IsNullOrWhiteSpace(config.TargetFramework) ? "net9.0" : config.TargetFramework;
        var tokens = SolutionV2Generator.BuildTokens(app, tfm, PackageVersionResolver.ResolveTargetMajor(tfm), provider, int.TryParse(config.ApiPort, out var port) ? port : SolutionV2Generator.StablePort(app));
        var manual = new List<string>();

        foreach (var template in TemplateRenderer.List("V2/solution"))
        {
            var relative = template["V2/solution/".Length..];
            if (!SkeletonFolders.Any(prefix => relative.StartsWith(prefix, StringComparison.Ordinal)) || relative.EndsWith("csproj.tpl", StringComparison.Ordinal))
                continue;

            var output = SolutionV2Generator.OutputPath(template, app);
            if (output.StartsWith("tests/", StringComparison.Ordinal))
                output = config.TestsRoot + output["tests".Length..];
            var target = Path.Combine(work, output);
            if (File.Exists(target))
                continue;

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllText(target, TemplateRenderer.RenderTemplate(template, tokens), new UTF8Encoding(false));
        }

        MergePackageVersions(work, TemplateRenderer.RenderTemplate("V2/solution/Directory.Packages.props.tpl", tokens), tokens["ExtOptionsConfigurationVersion"], manual);
        foreach (var (layerTemplate, project) in new[]
                 {
                     ("V2/solution/src/Domain/Domain.csproj.tpl", $"src/{app}.Domain/{app}.Domain.csproj"),
                     ("V2/solution/src/Application/Application.csproj.tpl", $"src/{app}.Application/{app}.Application.csproj"),
                     ("V2/solution/src/Infrastructure/Infrastructure.csproj.tpl", $"src/{app}.Infrastructure/{app}.Infrastructure.csproj"),
                     ("V2/solution/tests/Domain.Tests/Domain.Tests.csproj.tpl", $"{config.TestsRoot}/{app}.Domain.Tests/{app}.Domain.Tests.csproj"),
                     ("V2/solution/tests/Application.Tests/Application.Tests.csproj.tpl", $"{config.TestsRoot}/{app}.Application.Tests/{app}.Application.Tests.csproj"),
                     ("V2/solution/tests/Infrastructure.Tests/Infrastructure.Tests.csproj.tpl", $"{config.TestsRoot}/{app}.Infrastructure.Tests/{app}.Infrastructure.Tests.csproj"),
                 })
        {
            MergePackageReferences(Path.Combine(work, project), TemplateRenderer.RenderTemplate(layerTemplate, tokens), manual);
        }

        EnsureProjectReference(Path.Combine(work, $"src/{app}.Infrastructure/{app}.Infrastructure.csproj"), $"../{app}.Application/{app}.Application.csproj");
        EnsureProjectReference(Path.Combine(work, $"src/{app}.Api/{app}.Api.csproj"), $"../{app}.Infrastructure/{app}.Infrastructure.csproj");
        EnsureInternalsVisibleTo(Path.Combine(work, $"src/{app}.Infrastructure/{app}.Infrastructure.csproj"), $"{app}.Infrastructure.Tests");
        EnsureInternalsVisibleTo(Path.Combine(work, $"src/{app}.Application/{app}.Application.csproj"), $"{app}.Application.Tests");
        WriteSettings(work, app, provider, tokens);
        WireProgram(Path.Combine(work, $"src/{app}.Api/Program.cs"), app, manual);

        foreach (var note in manual)
            DotNetArch.Core.Hosting.ToolHost.Info("manual: " + note);
        DotNetArch.Core.Hosting.ToolHost.Success("Persistence skeleton added.", provider);
    }

    // ---- packages ------------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Adds the central versions the skeleton needs and raises (never lowers) the ones the service already pins: the EF Core packages require
    /// Microsoft.Extensions.* at their own version or higher, so an older pin would be a package downgrade.
    /// </summary>
    private static void MergePackageVersions(string work, string templateProps, string extensionsVersion, List<string> manual)
    {
        var path = Path.Combine(work, "Directory.Packages.props");
        if (!File.Exists(path))
        {
            manual.Add("Directory.Packages.props is missing: the packages of the skeleton have no central versions");
            return;
        }

        var text = File.ReadAllText(path);
        var add = new StringBuilder();
        foreach (Match version in Regex.Matches(templateProps, "<PackageVersion Include=\"(?<id>[^\"]+)\" Version=\"(?<version>[^\"]+)\" />", RegexOptions.CultureInvariant, Timeout))
        {
            var id = version.Groups["id"].Value;
            var existing = Regex.Match(text, $"<PackageVersion Include=\"{Regex.Escape(id)}\" Version=\"(?<version>[^\"]+)\" />", RegexOptions.CultureInvariant, Timeout);
            if (!existing.Success)
                add.Append("    ").Append(version.Value).Append('\n');
            else if (IsLower(existing.Groups["version"].Value, version.Groups["version"].Value))
                text = text.Replace(existing.Value, version.Value, StringComparison.Ordinal);
        }

        foreach (Match extension in Regex.Matches(text, "<PackageVersion Include=\"Microsoft\\.Extensions\\.[^\"]+\" Version=\"(?<version>[^\"]+)\" />", RegexOptions.CultureInvariant, Timeout).ToList())
        {
            if (IsLower(extension.Groups["version"].Value, extensionsVersion))
                text = text.Replace(extension.Value, extension.Value.Replace(extension.Groups["version"].Value, extensionsVersion, StringComparison.Ordinal), StringComparison.Ordinal);
        }

        // A project that overrides a pin with a lower version would be a downgrade against the raised pin.
        foreach (var project in Directory.EnumerateFiles(work, "*.csproj", SearchOption.AllDirectories).Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
        {
            var projectText = File.ReadAllText(project);
            var raised = Regex.Replace(projectText, "(?<pre><PackageReference Include=\"Microsoft\\.Extensions\\.[^\"]+\" VersionOverride=\")(?<version>[^\"]+)(?<post>\")", match =>
                IsLower(match.Groups["version"].Value, extensionsVersion) ? match.Groups["pre"].Value + extensionsVersion + match.Groups["post"].Value : match.Value, RegexOptions.CultureInvariant, Timeout);
            if (raised != projectText)
                File.WriteAllText(project, raised, new UTF8Encoding(false));
        }

        if (add.Length > 0 && text.Contains("</Project>", StringComparison.Ordinal))
            text = text.Replace("</Project>", $"  <ItemGroup Label=\"Persistence\">\n{add}  </ItemGroup>\n\n</Project>", StringComparison.Ordinal);
        File.WriteAllText(path, text, new UTF8Encoding(false));
    }

    private static bool IsLower(string existing, string wanted)
    {
        static Version? Parse(string value) => Version.TryParse(Regex.Match(value, @"^\d+(\.\d+){1,3}", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)).Value, out var version) ? version : null;
        var have = Parse(existing);
        var want = Parse(wanted);
        return have is not null && want is not null && have < want;
    }

    private static void MergePackageReferences(string project, string templateProject, List<string> manual)
    {
        if (!File.Exists(project))
        {
            manual.Add($"{Path.GetFileName(project)} was not found");
            return;
        }

        var existing = XDocument.Load(project, LoadOptions.PreserveWhitespace);
        var template = XDocument.Parse(templateProject);
        var changed = false;
        foreach (var kind in new[] { "PackageReference", "Using" })
        {
            var have = existing.Descendants(kind).Select(p => (string?)p.Attribute("Include")).ToHashSet(StringComparer.Ordinal);
            var missing = template.Descendants(kind).Where(p => !have.Contains((string?)p.Attribute("Include"))).ToList();
            if (missing.Count == 0)
                continue;

            var group = existing.Descendants("ItemGroup").FirstOrDefault(g => g.Elements(kind).Any());
            if (group is null)
            {
                group = new XElement("ItemGroup");
                existing.Root!.Add(new XText("  "), group, new XText("\n\n"));
            }

            foreach (var item in missing)
                group.Add(new XText("\n    "), item);
            group.Add(new XText("\n  "));
            changed = true;
        }

        if (changed)
            File.WriteAllText(project, existing.ToString(SaveOptions.DisableFormatting), new UTF8Encoding(false));
    }

    private static void EnsureProjectReference(string project, string include)
    {
        if (!File.Exists(project))
            return;

        var text = File.ReadAllText(project);
        if (text.Contains(include, StringComparison.OrdinalIgnoreCase) || text.Contains(include.Replace('/', '\\'), StringComparison.OrdinalIgnoreCase))
            return;

        text = text.Replace("</Project>", $"  <ItemGroup>\n    <ProjectReference Include=\"{include}\" />\n  </ItemGroup>\n\n</Project>", StringComparison.Ordinal);
        File.WriteAllText(project, text, new UTF8Encoding(false));
    }

    private static void EnsureInternalsVisibleTo(string project, string assembly)
    {
        if (!File.Exists(project))
            return;

        var text = File.ReadAllText(project);
        if (text.Contains($"InternalsVisibleTo Include=\"{assembly}\"", StringComparison.Ordinal))
            return;

        text = text.Replace("</Project>", $"  <ItemGroup>\n    <InternalsVisibleTo Include=\"{assembly}\" />\n  </ItemGroup>\n\n</Project>", StringComparison.Ordinal);
        File.WriteAllText(project, text, new UTF8Encoding(false));
    }

    // ---- settings and host ---------------------------------------------------------------------------------------------------

    private static void WriteSettings(string work, string app, string provider, IReadOnlyDictionary<string, string> tokens)
    {
        var api = Path.Combine(work, "src", $"{app}.Api");
        foreach (var file in new[] { "appsettings.json", "appsettings.example.json" })
        {
            var path = Path.Combine(api, file);
            if (!File.Exists(path))
                continue;

            var root = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            if (root["Database"] is not null)
                continue;

            root["Database"] = new System.Text.Json.Nodes.JsonObject { ["Provider"] = provider, ["MigrateOnStartup"] = false };
            File.WriteAllText(path, root.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }) + "\n", new UTF8Encoding(false));
        }

        var example = Path.Combine(api, ".env.example");
        if (File.Exists(example) && !File.ReadAllText(example).Contains("DATABASE_CONNECTION_STRING=", StringComparison.Ordinal))
        {
            File.AppendAllText(example, $"\n# Database connection string (secret)\nDATABASE_CONNECTION_STRING={tokens["ExampleConnectionString"]}\n");
        }
    }

    private static void WireProgram(string program, string app, List<string> manual)
    {
        if (!File.Exists(program))
        {
            manual.Add("Program.cs of the Api project was not found");
            return;
        }

        var text = File.ReadAllText(program);
        if (text.Contains("AddInfrastructure(", StringComparison.Ordinal))
            return;

        // The generated Application layer registers MediatR, the validators and the validation behaviour; a host that did it inline hands that over.
        var inline = new Regex(@"builder\.Services\.AddMediatR\(cfg =>\s*\{\s*cfg\.RegisterServicesFromAssembly\(typeof\(AssemblyMarker\)\.Assembly\);\s*\}\);\s*builder\.Services\.AddValidatorsFromAssemblyContaining<AssemblyMarker>\(\);", RegexOptions.CultureInvariant, Timeout);
        var nl = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = $"builder.Services.AddApplication();{nl}builder.Services.AddInfrastructure(builder.Configuration);";
        var match = inline.Match(text);
        if (match.Success)
            text = text.Remove(match.Index, match.Length).Insert(match.Index, lines);
        else if (Regex.Match(text, @"^[ \t]*(?:var|WebApplication)[ \t]+app[ \t]*=[ \t]*builder\.Build\(\)", RegexOptions.Multiline | RegexOptions.CultureInvariant, Timeout) is { Success: true } build)
            text = text.Insert(build.Index, lines + nl + nl);
        else
        {
            manual.Add("Program.cs has a shape the tool does not patch: add builder.Services.AddApplication(); builder.Services.AddInfrastructure(builder.Configuration); and await app.Services.InitializeInfrastructureAsync(); by hand");
            return;
        }

        var run = Regex.Match(text, @"^[ \t]*(?:await[ \t]+)?app\.Run(?:Async)?\(", RegexOptions.Multiline | RegexOptions.CultureInvariant, Timeout);
        if (run.Success)
            text = text.Insert(run.Index, $"await app.Services.InitializeInfrastructureAsync();{nl}{nl}");
        foreach (var ns in new[] { $"{app}.Application", $"{app}.Infrastructure" })
        {
            if (!text.Contains($"using {ns};", StringComparison.Ordinal))
            {
                var last = Regex.Matches(text, @"^using [^\r\n]+;", RegexOptions.Multiline | RegexOptions.CultureInvariant, Timeout).LastOrDefault();
                text = last is null ? $"using {ns};{nl}{text}" : text.Insert(last.Index + last.Length, $"{nl}using {ns};");
            }
        }

        File.WriteAllText(program, text, new UTF8Encoding(false));
    }
}
