using System.Text;
using System.Text.RegularExpressions;
using DotNetArch.Core.Templating;

namespace DotNetArch.Core.Operations;

/// <summary>
/// <c>scaffold_kit</c> and <c>scaffold_provider</c>: the tool creates the shared packages (Kits) the services plug their ports into, the same way it creates the
/// services. A Kit area has an Abstractions package (ports and data contracts), a Core package (registry, selection, DI) and one package per external
/// provider, plus a test project, bilingual docs and the package list entry; nothing is written by hand twice. Plan first; writes only with apply.
/// </summary>
internal static class KitOperations
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);

    public static IReadOnlyList<OperationDefinition> All => new[] { NewKit, AddProvider };

    private static OperationDefinition NewKit { get; } = new(
        "scaffold_kit",
        "Create a Kit area in the Kit repository: Abstractions and Core packages, a test project, bilingual README, AGENTS and specs, and the entry in Corevia.Kit.packages.proj. A Kit that exists is refused. Plan first; writes only with apply.",
        OperationKind.Mutating,
        new[]
        {
            new OperationParameter("path", "Root of the Kit repository (the folder with packages/).", Required: true, Positional: true, Alias: "output", Default: "@saved"),
            new OperationParameter("name", "Name of the area in PascalCase (packages become Corevia.Kit.<Name>.*).", Required: true),
            new OperationParameter("description", "One sentence: what the area gives every service.", Required: true),
            new OperationParameter("description_fa", "The same sentence in Persian (defaults to the English one)."),
            new OperationParameter("abstractions_only", "Do not create the Core package (a Kit that is only contracts).", ParameterType.Flag),
        },
        RunNewKit);

    private static OperationDefinition AddProvider { get; } = new(
        "scaffold_provider",
        "Add a provider package to a Kit area (Corevia.Kit.<Kit>.Providers.<Provider>): project, DI extension, reference from the test project, entry in Corevia.Kit.packages.proj and the lists in the docs. Plan first; writes only with apply.",
        OperationKind.Mutating,
        new[]
        {
            new OperationParameter("path", "Root of the Kit repository (the folder with packages/).", Required: true, Positional: true, Alias: "output", Default: "@saved"),
            new OperationParameter("kit", "Name of the existing Kit area.", Required: true),
            new OperationParameter("provider", "Provider name in PascalCase; dots group providers (Sms.Kavenegar).", Required: true),
            new OperationParameter("description", "One sentence: what the provider talks to.", Required: true),
            new OperationParameter("description_fa", "The same sentence in Persian (defaults to the English one)."),
        },
        RunAddProvider);

    private static OperationResult RunNewKit(OperationRequest request)
    {
        var path = KitRoot(request);
        var name = Pascal(request.Get("name"), "name");
        var description = request.Get("description")!;
        var tokens = Tokens(name, description, request.Get("description_fa") ?? description);
        var core = !request.Flag("abstractions_only");
        return ScaffoldRunner.Run(request, "scaffold_kit", path, skipMigrations: true, work =>
        {
            var area = Path.Combine(work, "packages", name);
            if (Directory.Exists(area))
                throw new InvalidOperationException($"The Kit area '{name}' already exists.");

            Write(Path.Combine(area, "AGENTS.md"), "Kit/AGENTS.md.tpl", tokens);
            Write(Path.Combine(area, "README.md"), "Kit/README.md.tpl", tokens);
            Write(Path.Combine(area, "README.fa.md"), "Kit/README.fa.md.tpl", tokens);
            foreach (var kind in new[] { "overview", "contracts", "acceptance", "changelog" })
            {
                Write(Path.Combine(area, "docs", "specs", kind + ".md"), $"Kit/specs/{kind}.md.tpl", tokens);
                Write(Path.Combine(area, "docs", "specs", kind + ".fa.md"), $"Kit/specs/{kind}.fa.md.tpl", tokens);
            }

            var abstractions = $"Corevia.Kit.{name}.Abstractions";
            Write(Path.Combine(area, abstractions, abstractions + ".csproj"), "Kit/Abstractions.csproj.tpl", tokens);
            Write(Path.Combine(area, abstractions, name + "AssemblyMarker.cs"), "Kit/AssemblyMarker.cs.tpl", tokens);
            var projects = new List<string> { $"packages/{name}/{abstractions}/{abstractions}.csproj" };
            if (core)
            {
                var coreName = $"Corevia.Kit.{name}.Core";
                Write(Path.Combine(area, coreName, coreName + ".csproj"), "Kit/Core.csproj.tpl", tokens);
                Write(Path.Combine(area, coreName, "DependencyInjection.cs"), "Kit/Core.DependencyInjection.cs.tpl", tokens);
                projects.Add($"packages/{name}/{coreName}/{coreName}.csproj");
            }

            var tests = Path.Combine(work, "tests", name, $"Corevia.Kit.{name}.Tests");
            Write(Path.Combine(tests, $"Corevia.Kit.{name}.Tests.csproj"), "Kit/Tests.csproj.tpl", tokens);
            Write(Path.Combine(tests, "SmokeTests.cs"), "Kit/SmokeTests.cs.tpl", tokens);
            if (core)
                AddTestReference(tests, name, $"Corevia.Kit.{name}.Core");
            Register(work, projects);
            DotNetArch.Core.Hosting.ToolHost.Success($"Kit {name} created.", core ? "Abstractions, Core, tests, docs" : "Abstractions, tests, docs");
        }, normalize: false);
    }

    private static OperationResult RunAddProvider(OperationRequest request)
    {
        var path = KitRoot(request);
        var kit = Pascal(request.Get("kit"), "kit");
        var provider = request.Get("provider") ?? throw new ArgumentException("Missing required value 'provider'.");
        if (!Regex.IsMatch(provider, @"^[A-Z][A-Za-z0-9]*(\.[A-Z][A-Za-z0-9]*)*$", RegexOptions.CultureInvariant, Timeout))
            throw new ArgumentException($"Provider '{provider}' must be PascalCase; dots group providers (Sms.Kavenegar).");

        var description = request.Get("description")!;
        var tokens = Tokens(kit, description, request.Get("description_fa") ?? description);
        tokens["Provider"] = provider;
        tokens["ProviderId"] = provider.Replace(".", string.Empty, StringComparison.Ordinal);
        return ScaffoldRunner.Run(request, "scaffold_provider", path, skipMigrations: true, work =>
        {
            var area = Path.Combine(work, "packages", kit);
            if (!Directory.Exists(area))
                throw new InvalidOperationException($"The Kit area '{kit}' does not exist; create it with scaffold_kit.");

            var package = $"Corevia.Kit.{kit}.Providers.{provider}";
            var folder = Path.Combine(area, package);
            if (Directory.Exists(folder))
                throw new InvalidOperationException($"The provider '{provider}' already exists in '{kit}'.");

            Write(Path.Combine(folder, package + ".csproj"), "Kit/Provider.csproj.tpl", tokens);
            Write(Path.Combine(folder, "DependencyInjection.cs"), "Kit/Provider.DependencyInjection.cs.tpl", tokens);
            AddTestReference(Path.Combine(work, "tests", kit, $"Corevia.Kit.{kit}.Tests"), kit, package);
            Register(work, new[] { $"packages/{kit}/{package}/{package}.csproj" });

            var line = $"- `{package}`: {description}";
            var lineFa = $"- `{package}`: {tokens["DescriptionFa"]}";
            Insert(Path.Combine(area, "README.md"), "<!-- dotnet-arch:providers -->", line);
            Insert(Path.Combine(area, "README.fa.md"), "<!-- dotnet-arch:providers -->", lineFa);
            Insert(Path.Combine(area, "docs", "specs", "contracts.md"), "<!-- dotnet-arch:provider-list -->", line);
            Insert(Path.Combine(area, "docs", "specs", "contracts.fa.md"), "<!-- dotnet-arch:provider-list -->", lineFa);
            DotNetArch.Core.Hosting.ToolHost.Success($"Provider {provider} added to {kit}.", package);
        }, normalize: false);
    }

    private static string KitRoot(OperationRequest request)
    {
        var path = Path.GetFullPath(request.Get("path") ?? throw new ArgumentException("Missing required value 'path'."));
        return Directory.Exists(Path.Combine(path, "packages")) ? path : throw new ArgumentException($"Not a Kit repository (no packages/ folder): {path}");
    }

    private static string Pascal(string? value, string parameter)
    {
        if (string.IsNullOrWhiteSpace(value) || !Regex.IsMatch(value, "^[A-Z][A-Za-z0-9]*$", RegexOptions.CultureInvariant, Timeout))
            throw new ArgumentException($"'{parameter}' must be PascalCase letters and digits (for example Delivery).");
        return value;
    }

    private static Dictionary<string, string> Tokens(string name, string description, string descriptionFa) => new()
    {
        ["Name"] = name,
        ["Description"] = description.Trim().TrimEnd('.'),
        ["DescriptionFa"] = descriptionFa.Trim().TrimEnd('.'),
        ["Date"] = DateTime.UtcNow.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
    };

    private static void Write(string target, string template, IReadOnlyDictionary<string, string> tokens)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(target, TemplateRenderer.RenderTemplate(template, tokens), new UTF8Encoding(false));
    }

    private static void Insert(string file, string marker, string line)
    {
        if (!File.Exists(file))
            return;
        var text = File.ReadAllText(file);
        if (text.Contains(marker, StringComparison.Ordinal))
            File.WriteAllText(file, text.Replace(marker, line + "\n" + marker, StringComparison.Ordinal), new UTF8Encoding(false));
    }

    private static void AddTestReference(string testFolder, string kit, string package)
    {
        var csproj = Path.Combine(testFolder, $"Corevia.Kit.{kit}.Tests.csproj");
        if (!File.Exists(csproj))
            return;
        const string marker = "    <!-- dotnet-arch:kit-projects";
        var text = File.ReadAllText(csproj);
        var reference = $"    <ProjectReference Include=\"../../../packages/{kit}/{package}/{package}.csproj\" />\n";
        if (!text.Contains(reference, StringComparison.Ordinal) && text.Contains(marker, StringComparison.Ordinal))
            File.WriteAllText(csproj, text.Replace(marker, reference + marker, StringComparison.Ordinal), new UTF8Encoding(false));
    }

    /// <summary>Adds the projects to the package list the pipeline restores, builds and packs (before the first target).</summary>
    private static void Register(string work, IEnumerable<string> projects)
    {
        var file = Path.Combine(work, "Corevia.Kit.packages.proj");
        if (!File.Exists(file))
            return;
        var text = File.ReadAllText(file);
        var add = new StringBuilder();
        foreach (var project in projects)
        {
            var line = $"    <ProjectFiles Include=\"$(MSBuildThisFileDirectory){project}\" />\n";
            if (!text.Contains(line, StringComparison.Ordinal))
                add.Append(line);
        }

        var itemGroupEnd = Regex.Match(text, @"^  </ItemGroup>\s*\n  <Target", RegexOptions.Multiline | RegexOptions.CultureInvariant, Timeout);
        if (add.Length > 0 && itemGroupEnd.Success)
            File.WriteAllText(file, text.Insert(itemGroupEnd.Index, add.ToString()), new UTF8Encoding(false));
    }
}
