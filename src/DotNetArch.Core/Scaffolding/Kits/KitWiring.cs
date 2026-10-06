using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace DotNetArch.Core.Scaffolding.Kits;

/// <summary>
/// Wires a generated kit into a layout-v2 solution: Application references the Abstractions, the Api composition root references Core and
/// the providers and registers them, and the configuration surface (appsettings, examples, secret contract) is extended.
/// </summary>
public static class KitWiring
{
    public static bool WireFromDisk(SolutionConfig config, string area)
    {
        var folder = Path.Combine(config.SolutionPath, "kits", area);
        var metadata = Path.Combine(folder, KitGenerator.MetadataFile);
        if (!File.Exists(metadata))
        {
            ToolHost.Error($"Kit '{area}' was not found.", $"Expected {Path.Combine("kits", area, KitGenerator.MetadataFile)}. Generate it with 'new kit'.");
            return false;
        }

        var info = JsonSerializer.Deserialize<KitInfo>(File.ReadAllText(metadata))
            ?? throw new InvalidOperationException($"{metadata} is empty.");
        return Wire(config, info);
    }

    public static bool Wire(SolutionConfig config, KitInfo kit)
    {
        if (!config.IsV2)
        {
            ToolHost.Error("Kits can only be wired into layout v2 solutions.");
            return false;
        }

        var app = config.SolutionName;
        var kitName = $"{kit.Prefix}.Kit.{kit.Area}";
        string Csproj(string part) => $"kits/{kit.Area}/{kitName}.{part}/{kitName}.{part}.csproj";

        // Application sees the contract only; the composition root sees Core and the providers.
        AddProjectReference(config, $"{app}.Application", Csproj("Abstractions"));
        AddProjectReference(config, $"{app}.Api", Csproj("Core"));
        foreach (var provider in kit.Providers)
            AddProjectReference(config, $"{app}.Api", Csproj($"Providers.{provider}"));

        RegisterInCompositionRoot(config, kit, kitName);
        MergeSettings(config, kit);
        AppendSecrets(config, kit);

        var projects = new[] { Csproj("Abstractions"), Csproj("Core") }.Concat(kit.Providers.Select(p => Csproj($"Providers.{p}")));
        foreach (var project in projects)
            ToolHost.RunCommand($"dotnet sln add {project} --solution-folder kits/{kit.Area}", config.SolutionPath, print: false);

        config.Kits[kit.Area] = string.Join(',', kit.Providers);
        if (string.IsNullOrWhiteSpace(config.KitPrefix))
            config.KitPrefix = kit.Prefix;
        ConfigManager.Save(config.SolutionPath, config);

        ToolHost.Success($"Kit {kit.Area} wired into {app}.", $"providers: {string.Join(", ", kit.Providers)}; select with {kit.Area}:Provider");
        return true;
    }

    // --- project references -------------------------------------------------------------------------------------------
    private static void AddProjectReference(SolutionConfig config, string project, string relativeToSolution)
    {
        var csproj = Path.Combine(config.SolutionPath, config.ProjectFile(project));
        var projectDir = Path.GetDirectoryName(csproj)!;
        var include = Path.GetRelativePath(projectDir, Path.Combine(config.SolutionPath, relativeToSolution)).Replace('\\', '/');

        var document = XDocument.Load(csproj, LoadOptions.PreserveWhitespace);
        var root = document.Root!;
        var exists = root.Descendants("ProjectReference").Any(reference => string.Equals((string?)reference.Attribute("Include"), include, StringComparison.OrdinalIgnoreCase));
        if (exists)
            return;

        var group = root.Elements("ItemGroup").LastOrDefault(g => g.Elements("ProjectReference").Any());
        if (group is null)
        {
            group = new XElement("ItemGroup");
            root.Add(new XText("\n  "), group, new XText("\n"));
        }

        group.Add(new XText("  "), new XElement("ProjectReference", new XAttribute("Include", include)), new XText("\n  "));
        File.WriteAllText(csproj, document.ToString(SaveOptions.DisableFormatting).Replace("\r\n", "\n"), new System.Text.UTF8Encoding(false));
    }

    // --- Program wiring -----------------------------------------------------------------------------------------------
    private static void RegisterInCompositionRoot(SolutionConfig config, KitInfo kit, string kitName)
    {
        var path = Path.Combine(config.SolutionPath, config.ProjectDirectory($"{config.SolutionName}.Api"), "Configuration", "KitRegistrations.cs");
        if (!File.Exists(path))
            throw new InvalidOperationException($"{path} is missing; it is created with every layout v2 solution.");

        var text = File.ReadAllText(path);
        var usings = new List<string> { $"using {kitName}.Core;" };
        usings.AddRange(kit.Providers.Select(provider => $"using {kitName}.Providers.{provider};"));

        var registrations = new List<string>();
        registrations.AddRange(kit.Providers.Select(provider => $"        services.Add{provider}{kit.Area}Provider();"));
        registrations.Add($"        services.Add{kit.Area}Kit(configuration);");

        if (text.Contains($"Add{kit.Area}Kit(configuration)", StringComparison.Ordinal))
        {
            ToolHost.Info($"Kit {kit.Area} is already registered in KitRegistrations.cs.");
            return;
        }

        text = InsertBefore(text, "// <dotnet-arch:kit-usings>", string.Concat(usings.Where(u => !text.Contains(u, StringComparison.Ordinal)).Select(u => u + "\n")), keepIndent: false);
        text = InsertBefore(text, "// <dotnet-arch:kits>", string.Join("\n", registrations) + "\n", keepIndent: false, markerIndent: "        ");
        File.WriteAllText(path, text, new System.Text.UTF8Encoding(false));
    }

    private static string InsertBefore(string text, string marker, string insertion, bool keepIndent, string markerIndent = "")
    {
        var index = text.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0)
            throw new InvalidOperationException($"Marker '{marker}' was not found; add the registration manually.");

        // Insert at the start of the marker's line so the marker stays last.
        var lineStart = text.LastIndexOf('\n', index) + 1;
        return text.Insert(lineStart, insertion);
    }

    // --- configuration surface ----------------------------------------------------------------------------------------
    private static void MergeSettings(SolutionConfig config, KitInfo kit)
    {
        var apiDir = Path.Combine(config.SolutionPath, config.ProjectDirectory($"{config.SolutionName}.Api"));
        foreach (var file in new[] { "appsettings.json", "appsettings.example.json" })
        {
            var path = Path.Combine(apiDir, file);
            if (!File.Exists(path))
                continue;

            var root = JsonNode.Parse(File.ReadAllText(path), documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip })!.AsObject();
            foreach (var (sectionPath, values) in kit.Settings)
            {
                var target = root;
                foreach (var part in sectionPath.Split(':'))
                {
                    if (target[part] is not JsonObject child)
                        target[part] = child = new JsonObject();
                    target = child;
                }

                foreach (var (key, value) in values)
                {
                    if (!target.ContainsKey(key))
                        target[key] = ToJson(value);
                }
            }

            File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }).Replace("\r\n", "\n") + "\n", new System.Text.UTF8Encoding(false));
        }
    }

    private static JsonNode ToJson(string value) =>
        bool.TryParse(value, out var boolean) ? JsonValue.Create(boolean)
        : long.TryParse(value, out var number) ? JsonValue.Create(number)
        : JsonValue.Create(value)!;

    private static void AppendSecrets(SolutionConfig config, KitInfo kit)
    {
        if (kit.Secrets.Count == 0)
            return;

        var apiDir = Path.Combine(config.SolutionPath, config.ProjectDirectory($"{config.SolutionName}.Api"));
        var example = Path.Combine(apiDir, ".env.example");
        if (File.Exists(example))
        {
            var text = File.ReadAllText(example);
            var addition = new System.Text.StringBuilder();
            foreach (var (key, doc) in kit.Secrets)
            {
                if (!text.Contains($"{key}=", StringComparison.Ordinal))
                    addition.Append($"\n# {kit.Area} kit - {doc}\n{key}=\n");
            }
            File.WriteAllText(example, text.TrimEnd('\n') + "\n" + addition, new System.Text.UTF8Encoding(false));
        }

        var contract = Path.Combine(apiDir, "Configuration", "ConfigurationContract.cs");
        if (File.Exists(contract))
        {
            var text = File.ReadAllText(contract);
            var lines = string.Concat(kit.Secrets.Keys
                .Where(key => !text.Contains($"\"{key}\"", StringComparison.Ordinal))
                .Select(key => $"        \"{key}\",\n"));
            if (lines.Length > 0)
                File.WriteAllText(contract, InsertBefore(text, "// <dotnet-arch:secret-keys>", lines, keepIndent: false), new System.Text.UTF8Encoding(false));
        }
    }
}
