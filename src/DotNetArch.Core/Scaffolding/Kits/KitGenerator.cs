using System.Text;
using System.Text.Json;
using DotNetArch.Core.Scaffolding.V2;
using DotNetArch.Core.Templating;

namespace DotNetArch.Core.Scaffolding.Kits;

public sealed record KitRequest(
    string Root,
    string Area,
    IReadOnlyList<string> Providers,
    string Prefix,
    int Major = 8,
    bool WithTests = false);

/// <summary>What was generated; also persisted as <c>kit.json</c> so <c>add kit</c> can wire the kit later.</summary>
public sealed record KitInfo(
    string Area,
    string Prefix,
    IReadOnlyList<string> Providers,
    IReadOnlyDictionary<string, string> Secrets,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Settings);

/// <summary>Creates an independent kit under <c>kits/&lt;Area&gt;</c>: Abstractions, Core and one package per provider, with docs and specs.</summary>
public static class KitGenerator
{
    public const string MetadataFile = "kit.json";

    public static KitInfo Generate(KitRequest request)
    {
        var area = NormalizeArea(request.Area);
        var prefix = Identifier.RequireSolutionName(request.Prefix);
        Identifier.RequirePath(request.Root, "output path");

        var recipe = KitRecipes.Find(area);
        if (recipe is not null)
            area = recipe.Area;

        var providers = ResolveProviders(recipe, request.Providers);
        var folder = Path.Combine(request.Root, "kits", area);
        if (Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any())
        {
            throw new InvalidOperationException(
                $"Kit folder '{Path.Combine("kits", area)}' already exists. Add providers by hand or remove the folder first.");
        }

        var major = request.Major;
        var tokens = BuildTokens(area, prefix, major, recipe, providers);
        var writer = new FileWriter(folder);
        var kit = $"{prefix}.Kit.{area}";
        var projects = new List<string>();

        // --- shared kit files ----------------------------------------------------------------------------------------
        foreach (var name in new[] { "Directory.Build.props", "Directory.Packages.props", "README.md", "README.fa.md", "AGENTS.md" })
            writer.Write(name, TemplateRenderer.RenderTemplate($"V2/kit/common/{name}.tpl", tokens));
        writer.Write("scripts/pack.sh", TemplateRenderer.RenderTemplate("V2/kit/common/pack.sh.tpl", tokens));
        foreach (var doc in new[] { "overview", "contracts", "acceptance", "changelog" })
        {
            writer.Write($"docs/specs/{doc}.md", TemplateRenderer.RenderTemplate($"V2/kit/common/docs/{doc}.md.tpl", tokens));
            writer.Write($"docs/specs/{doc}.fa.md", TemplateRenderer.RenderTemplate($"V2/kit/common/docs/{doc}.fa.md.tpl", tokens));
        }

        // --- Abstractions --------------------------------------------------------------------------------------------
        var abstractions = $"{kit}.Abstractions";
        projects.Add($"{abstractions}/{abstractions}.csproj");
        writer.Write($"{abstractions}/{abstractions}.csproj", TemplateRenderer.RenderTemplate("V2/kit/common/Abstractions/Abstractions.csproj.tpl", tokens));
        writer.Write($"{abstractions}/{area}Exception.cs", TemplateRenderer.RenderTemplate("V2/kit/common/Abstractions/KitException.cs.tpl", tokens));
        WritePart(writer, recipe is null ? "V2/kit/generic/Abstractions" : $"V2/kit/recipes/{area}/Abstractions", abstractions, tokens, recipe is null ? "Contract.cs.tpl" : null, recipe is null ? $"I{area}.cs" : null);

        // --- Core ----------------------------------------------------------------------------------------------------
        var core = $"{kit}.Core";
        projects.Add($"{core}/{core}.csproj");
        writer.Write($"{core}/{core}.csproj", TemplateRenderer.RenderTemplate("V2/kit/common/Core/Core.csproj.tpl", tokens));
        writer.Write($"{core}/I{area}Provider.cs", TemplateRenderer.RenderTemplate("V2/kit/common/Core/IProvider.cs.tpl", tokens));
        writer.Write($"{core}/{area}ProviderResolver.cs", TemplateRenderer.RenderTemplate("V2/kit/common/Core/ProviderResolver.cs.tpl", tokens));
        writer.Write($"{core}/DependencyInjection.cs", TemplateRenderer.RenderTemplate("V2/kit/common/Core/DependencyInjection.cs.tpl", tokens));
        if (recipe is not null)
            WritePart(writer, $"V2/kit/recipes/{area}/Core", core, tokens);

        // --- Providers -----------------------------------------------------------------------------------------------
        var providerProjects = new List<string>();
        foreach (var provider in providers)
        {
            var providerTokens = new Dictionary<string, string>(tokens)
            {
                ["Provider"] = provider,
                ["ProviderUpper"] = provider.ToUpperInvariant(),
                ["ProviderPackageReferences"] = PackageReferences(recipe?.FindProvider(provider)?.Packages),
            };
            var project = $"{kit}.Providers.{provider}";
            projects.Add($"{project}/{project}.csproj");
            providerProjects.Add($"{project}/{project}.csproj");
            writer.Write($"{project}/{project}.csproj", TemplateRenderer.RenderTemplate("V2/kit/common/Provider/Provider.csproj.tpl", providerTokens));
            if (recipe is null)
                writer.Write($"{project}/{provider}{area}Provider.cs", TemplateRenderer.RenderTemplate("V2/kit/generic/Provider/Provider.cs.tpl", providerTokens));
            else
            {
                var providerFolder = $"V2/kit/recipes/{area}/Providers/{provider}";
                if (TemplateRenderer.List(providerFolder).Count == 0)
                    providerFolder = $"V2/kit/recipes/{area}/Providers/_default";
                WritePart(writer, providerFolder, project, providerTokens);
            }
        }

        // --- Tests (opt-in) ------------------------------------------------------------------------------------------
        if (request.WithTests)
        {
            var tests = $"{kit}.Tests";
            var references = new StringBuilder();
            foreach (var project in projects)
                references.Append($"    <ProjectReference Include=\"../../{project}\" />\n");
            var testTokens = new Dictionary<string, string>(tokens) { ["TestProjectReferences"] = references.ToString() };
            projects.Add($"tests/{tests}/{tests}.csproj");
            writer.Write($"tests/{tests}/{tests}.csproj", TemplateRenderer.RenderTemplate("V2/kit/common/Tests/Tests.csproj.tpl", testTokens));
            writer.Write($"tests/{tests}/ProviderSelectionTests.cs", TemplateRenderer.RenderTemplate("V2/kit/common/Tests/ResolverTests.cs.tpl", testTokens));
            if (recipe is not null)
                WritePart(writer, $"V2/kit/recipes/{area}/Tests", $"tests/{tests}", testTokens);
        }

        // --- metadata + solution file ---------------------------------------------------------------------------------
        var info = BuildInfo(area, prefix, recipe, providers);
        writer.Write(MetadataFile, JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true }) + "\n");

        ToolHost.RunCommand($"dotnet new sln -n {kit} --force", folder);
        foreach (var project in projects)
            ToolHost.RunCommand($"dotnet sln {kit}.sln add {project}", folder);

        ToolHost.Success($"Kit {kit} generated ({writer.Created.Count} files, providers: {string.Join(", ", providers)}).");
        return info;
    }

    /// <summary>Renders every template of a recipe part (<c>X.cs.tpl</c> becomes <c>X.cs</c>) into the project folder.</summary>
    private static void WritePart(FileWriter writer, string templateFolder, string project, Dictionary<string, string> tokens, string? onlyTemplate = null, string? renameTo = null)
    {
        foreach (var template in TemplateRenderer.List(templateFolder))
        {
            var name = template[(template.LastIndexOf('/') + 1)..];
            if (onlyTemplate is not null && !name.Equals(onlyTemplate, StringComparison.Ordinal))
                continue;

            // Files in nested folders keep their relative path below the part folder.
            var relative = template[(templateFolder.Length + 1)..];
            relative = relative.EndsWith(".tpl", StringComparison.Ordinal) ? relative[..^4] : relative;
            if (renameTo is not null)
                relative = renameTo;
            writer.Write($"{project}/{relative}", TemplateRenderer.RenderTemplate(template, tokens));
        }
    }

    public static string NormalizeArea(string area)
    {
        Identifier.RequireIdentifier(area, "kit area");
        return char.ToUpperInvariant(area[0]) + area[1..];
    }

    private static List<string> ResolveProviders(KitRecipe? recipe, IReadOnlyList<string> requested)
    {
        var names = requested.Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name.Trim()).ToList();
        if (recipe is null)
        {
            if (names.Count == 0)
                names.Add("Default");
            foreach (var name in names)
                Identifier.RequireIdentifier(name, "provider name");
            return names.Select(name => char.ToUpperInvariant(name[0]) + name[1..]).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        if (names.Count == 0)
            return recipe.Providers.Select(provider => provider.Name).ToList();

        var resolved = new List<string>();
        foreach (var name in names)
        {
            var provider = recipe.FindProvider(name)
                ?? throw new ArgumentException(
                    $"'{name}' is not a built-in {recipe.Area} provider. Built-in providers: {string.Join(", ", recipe.Providers.Select(p => p.Name))}.");
            if (!resolved.Contains(provider.Name, StringComparer.Ordinal))
                resolved.Add(provider.Name);
        }
        return resolved;
    }

    private static KitInfo BuildInfo(string area, string prefix, KitRecipe? recipe, IReadOnlyList<string> providers)
    {
        var secrets = new Dictionary<string, string>();
        var settings = new Dictionary<string, IReadOnlyDictionary<string, string>>
        {
            [area] = new Dictionary<string, string> { ["Provider"] = providers[0] }
        };

        foreach (var provider in providers)
        {
            var providerRecipe = recipe?.FindProvider(provider);
            if (providerRecipe is null)
                continue;

            foreach (var (key, doc) in providerRecipe.Secrets)
                secrets[key] = $"{provider}: {doc}";
            if (providerRecipe.Settings.Count > 0)
                settings[$"{area}:{provider}"] = providerRecipe.Settings;
        }

        return new KitInfo(area, prefix, providers, secrets, settings);
    }

    private static Dictionary<string, string> BuildTokens(string area, string prefix, int major, KitRecipe? recipe, IReadOnlyList<string> providers)
    {
        var providerRecipes = providers.Select(name => recipe?.FindProvider(name)).ToList();
        var info = BuildInfo(area, prefix, recipe, providers);

        var versions = new StringBuilder();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var package in (recipe?.CorePackages ?? Array.Empty<KitPackage>())
                     .Concat(providerRecipes.Where(p => p is not null).SelectMany(p => p!.Packages)))
        {
            if (seen.Add(package.Id))
                versions.Append($"    <PackageVersion Include=\"{package.Id}\" Version=\"{package.Version(major)}\" />\n");
        }

        var providerPackages = new StringBuilder();
        var registration = new StringBuilder();
        foreach (var provider in providers)
        {
            var description = recipe?.FindProvider(provider)?.Description ?? $"{provider} provider";
            providerPackages.Append($"- `{prefix}.Kit.{area}.Providers.{provider}` - {description}.\n");
            registration.Append($"services.Add{provider}{area}Provider();\n");
        }

        var config = new StringBuilder();
        var secrets = new StringBuilder();
        foreach (var (provider, providerRecipe) in providers.Zip(providerRecipes))
        {
            if (providerRecipe is null)
                continue;
            foreach (var (key, doc) in providerRecipe.SettingDocs)
                config.Append($"| `{area}:{provider}:{key}` | {doc} |\n");
        }
        foreach (var (key, doc) in info.Secrets)
            secrets.Append($"| `{key}` | {doc} |\n");

        var contractSummary = recipe?.ContractSummary ?? $"`I{area}` (replace the sample member with the operations your services need) and `{area}Exception`.";
        return new Dictionary<string, string>
        {
            ["Area"] = area,
            ["AreaSnake"] = Naming.ToKebabCase(area).Replace('-', '_'),
            ["AreaVariable"] = char.ToLowerInvariant(area[0]) + area[1..],
            ["AreaDescription"] = recipe?.Description ?? $"Provider-based {area} capability.",
            ["AreaDescriptionFa"] = recipe?.DescriptionFa ?? $"قابلیت {area} مبتنی بر provider.",
            ["Prefix"] = prefix,
            ["Tfm"] = $"net{major}.0",
            ["Authors"] = prefix,
            ["PackageTags"] = $"kit;{area.ToLowerInvariant()};dotnet",
            ["ExtConfigurationVersion"] = PackageCatalog.ExtOptionsConfiguration(major),
            ["ExtDependencyInjectionVersion"] = PackageCatalog.ExtDependencyInjection(major),
            ["ExtDependencyInjectionImplVersion"] = major >= 9 ? "9.0.20" : "8.0.1",
            ["KitPackageVersions"] = versions.ToString(),
            ["AbstractionsPackages"] = string.Empty,
            ["CorePackageReferences"] = PackageReferences(recipe?.CorePackages),
            ["ProviderPackageReferences"] = string.Empty,
            ["ProviderPackageList"] = providerPackages.ToString().TrimEnd('\n'),
            ["ProviderNames"] = string.Join(", ", providers),
            ["RegistrationSnippet"] = registration.ToString().TrimEnd('\n'),
            ["ConfigTable"] = config.Length == 0 ? "| (none) | |\n" : config.ToString(),
            ["SecretTable"] = secrets.Length == 0 ? "| (none) | |\n" : secrets.ToString(),
            ["ContractSummary"] = contractSummary,
            ["TestProjectReferences"] = string.Empty,
            ["Provider"] = string.Empty,
            ["ProviderUpper"] = string.Empty,
        };
    }

    private static string PackageReferences(IEnumerable<KitPackage>? packages) =>
        packages is null ? string.Empty : string.Concat(packages.Select(package => $"    <PackageReference Include=\"{package.Id}\" />\n"));
}
