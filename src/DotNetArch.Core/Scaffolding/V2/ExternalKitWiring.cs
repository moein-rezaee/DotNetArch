using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using DotNetArch.Core.NetArch;

namespace DotNetArch.Core.Scaffolding.V2;

/// <summary>
/// Wires the published kits a profile lists (<see cref="ProfileDefinition.ExternalKits"/>) into a solution: package versions and references, the
/// registration lines of each host (api, mcp, cli) at the markers of its <c>Program.cs</c>, settings, secrets and the generated parts a kit replaces.
/// The tool names no kit: everything comes from the profile (D-22, D-39). Running it twice changes nothing.
/// </summary>
public static class ExternalKitWiring
{
    private const string UsingsMarker = "// <dotnet-arch:kit-usings>";
    private const string BootstrapMarker = "// <dotnet-arch:kit-bootstrap>";
    private const string BuilderMarker = "// <dotnet-arch:kit-builder>";
    private const string AppMarker = "// <dotnet-arch:kit-app>";
    private const string SecretKeysMarker = "// <dotnet-arch:secret-keys>";

    private static readonly Dictionary<string, string> HostSuffix = new(StringComparer.OrdinalIgnoreCase)
    {
        ["api"] = "Api",
        ["mcp"] = "Mcp",
        ["cli"] = "Cli",
        ["application"] = "Application",
        ["infrastructure"] = "Infrastructure",
    };

    public static void Apply(SolutionConfig config, ProfileDefinition profile)
    {
        if (profile.ExternalKits.Count == 0)
            return;

        foreach (var kit in profile.ExternalKits)
        {
            Identifier.RequireIdentifier(kit.Area, "kit area");
            WritePackages(config, kit);
            foreach (var (host, wiring) in kit.Hosts)
            {
                var project = HostProject(config, host);
                if (!Directory.Exists(Path.Combine(config.SolutionPath, project)))
                    continue;

                WireProgram(config, project, wiring);
                WriteSettings(config, project, kit.Settings);
                WriteSecrets(config, project, kit, host);
                WriteTestSettings(config, host, kit.TestSettings);
            }

            foreach (var replaced in kit.Replaces)
                Replace(config, replaced);
        }

        ToolHost.Success($"Kits wired from the profile ({profile.ExternalKits.Count}).", string.Join(", ", profile.ExternalKits.Select(k => k.Area)));
    }

    private static string HostProject(SolutionConfig config, string host)
    {
        if (!HostSuffix.TryGetValue(host, out var suffix))
            throw new ArgumentException($"Unknown host '{host}' in the profile. Use api, mcp or cli.");
        return $"src/{config.SolutionName}.{suffix}";
    }

    // ---- packages ------------------------------------------------------------------------------------------------------------

    private static void WritePackages(SolutionConfig config, ExternalKit kit)
    {
        var props = Path.Combine(config.SolutionPath, "Directory.Packages.props");
        foreach (var package in kit.Packages)
        {
            if (!Regex.IsMatch(package.Id, @"^[A-Za-z][A-Za-z0-9_.]*$") || !Regex.IsMatch(package.Version, @"^[0-9A-Za-z.\-+]+$"))
                throw new ArgumentException($"Invalid package '{package.Id}' {package.Version} in the profile.");

            if (File.Exists(props))
            {
                var text = File.ReadAllText(props);
                var existing = Regex.Match(text, $"<PackageVersion Include=\"{Regex.Escape(package.Id)}\" Version=\"([^\"]*)\" />", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
                var line = $"    <PackageVersion Include=\"{package.Id}\" Version=\"{package.Version}\" />\n";
                if (existing.Success)
                {
                    // The profile is the source of truth for kit versions: a newer profile upgrades the pin.
                    if (existing.Groups[1].Value != package.Version)
                        text = text.Replace(existing.Value, line.Trim(), StringComparison.Ordinal);
                }
                else
                {
                    text = text.Contains("<ItemGroup Label=\"Kits\">", StringComparison.Ordinal)
                        ? text.Replace("<ItemGroup Label=\"Kits\">\n", "<ItemGroup Label=\"Kits\">\n" + line, StringComparison.Ordinal)
                        : text.Replace("</Project>", $"  <ItemGroup Label=\"Kits\">\n{line}  </ItemGroup>\n\n</Project>", StringComparison.Ordinal);
                }

                File.WriteAllText(props, text, new UTF8Encoding(false));
            }

            foreach (var project in package.Projects)
            {
                var csproj = Path.Combine(config.SolutionPath, HostProject(config, project), $"{config.SolutionName}.{HostSuffix[project]}.csproj");
                AddPackageReference(csproj, package.Id);
            }
        }
    }

    private static void AddPackageReference(string csproj, string id)
    {
        if (!File.Exists(csproj))
            return;

        var text = File.ReadAllText(csproj);
        if (text.Contains($"Include=\"{id}\"", StringComparison.Ordinal))
            return;

        var reference = $"    <PackageReference Include=\"{id}\" />\n";
        var first = text.IndexOf("<PackageReference ", StringComparison.Ordinal);
        text = first >= 0
            ? text.Insert(text.LastIndexOf('\n', first) + 1, reference)
            : text.Replace("</Project>", $"  <ItemGroup>\n{reference}  </ItemGroup>\n\n</Project>", StringComparison.Ordinal);
        File.WriteAllText(csproj, text, new UTF8Encoding(false));
    }

    // ---- Program.cs ----------------------------------------------------------------------------------------------------------

    private static void WireProgram(SolutionConfig config, string project, KitHostWiring wiring)
    {
        var path = Path.Combine(config.SolutionPath, project, "Program.cs");
        if (!File.Exists(path))
            return;

        var text = File.ReadAllText(path);
        text = Insert(text, UsingsMarker, wiring.Usings.Select(u => $"using {Token(config, u)};"), "");
        text = Insert(text, BootstrapMarker, wiring.Bootstrap.Select(l => Token(config, l)), "");
        text = Insert(text, BuilderMarker, wiring.Builder.Select(l => Token(config, l)), "");
        text = Insert(text, AppMarker, wiring.App.Select(l => Token(config, l)), "");
        File.WriteAllText(path, text, new UTF8Encoding(false));
    }

    /// <summary>Adds each line above the marker unless the file already has it (a second run changes nothing).</summary>
    private static string Insert(string text, string marker, IEnumerable<string> lines, string indent)
    {
        var index = text.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0)
            return text;

        var add = lines.Where(line => !text.Contains(line, StringComparison.Ordinal)).Select(line => indent + line + "\n").ToList();
        return add.Count == 0 ? text : text.Insert(index, string.Concat(add));
    }

    private static string Token(SolutionConfig config, string line) => line.Replace("{{App}}", config.SolutionName, StringComparison.Ordinal);

    // ---- settings and secrets ------------------------------------------------------------------------------------------------

    private static void WriteSettings(SolutionConfig config, string project, IReadOnlyDictionary<string, string> settings)
    {
        if (settings.Count == 0)
            return;

        foreach (var file in new[] { "appsettings.json", "appsettings.example.json" })
        {
            var path = Path.Combine(config.SolutionPath, project, file);
            if (!File.Exists(path))
                continue;

            var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            foreach (var (key, value) in settings)
            {
                var segments = key.Split(':', StringSplitOptions.RemoveEmptyEntries);
                var node = root;
                foreach (var segment in segments[..^1])
                {
                    if (node[segment] is not JsonObject child)
                        node[segment] = child = new JsonObject();
                    node = child;
                }

                if (node[segments[^1]] is null)
                    node[segments[^1]] = bool.TryParse(value, out var flag) ? flag : int.TryParse(value, out var number) ? number : value;
            }

            File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n", new UTF8Encoding(false));
        }
    }

    private static void WriteSecrets(SolutionConfig config, string project, ExternalKit kit, string host)
    {
        if (kit.Secrets.Count == 0)
            return;

        var example = Path.Combine(config.SolutionPath, project, ".env.example");
        if (File.Exists(example))
        {
            var text = File.ReadAllText(example);
            var missing = kit.Secrets.Where(key => !Regex.IsMatch(text, $"^{Regex.Escape(key)}=", RegexOptions.Multiline | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1))).ToList();
            if (missing.Count > 0)
                File.WriteAllText(example, text.TrimEnd('\n') + $"\n\n# {kit.Area} kit\n" + string.Concat(missing.Select(key => $"{key}=\n")), new UTF8Encoding(false));
        }

        if (host.Equals("api", StringComparison.OrdinalIgnoreCase))
        {
            var contract = Path.Combine(config.SolutionPath, project, "Configuration", "ConfigurationContract.cs");
            if (File.Exists(contract))
            {
                var text = File.ReadAllText(contract);
                var index = text.IndexOf(SecretKeysMarker, StringComparison.Ordinal);
                var lines = kit.Secrets.Where(key => !text.Contains($"\"{key}\"", StringComparison.Ordinal)).Select(key => $"        \"{key}\",\n").ToList();
                if (index >= 0 && lines.Count > 0)
                    File.WriteAllText(contract, text.Insert(index - 8, string.Concat(lines)), new UTF8Encoding(false));
            }
        }
    }

    private static void WriteTestSettings(SolutionConfig config, string host, IReadOnlyDictionary<string, string> settings)
    {
        if (settings.Count == 0)
            return;

        var factory = host.ToLowerInvariant() switch
        {
            "api" => Path.Combine(config.SolutionPath, config.TestsRoot, $"{config.SolutionName}.Api.Tests", "Support", "ApiFactory.cs"),
            "mcp" => Path.Combine(config.SolutionPath, config.TestsRoot, $"{config.SolutionName}.Mcp.Tests", "McpFactory.cs"),
            _ => null,
        };
        if (factory is null || !File.Exists(factory))
            return;

        var text = File.ReadAllText(factory);
        const string marker = "// <dotnet-arch:kit-test-settings>";
        var index = text.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0)
            return;

        var lines = settings
            .Where(pair => !text.Contains($"UseSetting(\"{pair.Key}\"", StringComparison.Ordinal))
            .Select(pair => $"        builder.UseSetting(\"{pair.Key}\", \"{pair.Value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\");\n")
            .ToList();
        if (lines.Count > 0)
            File.WriteAllText(factory, text.Insert(text.LastIndexOf('\n', index) + 1, string.Concat(lines)), new UTF8Encoding(false));
    }

    // ---- replaced parts ------------------------------------------------------------------------------------------------------

    private static void Replace(SolutionConfig config, string part)
    {
        var api = $"src/{config.SolutionName}.Api";
        var extensions = Path.Combine(config.SolutionPath, api, "Configuration", "ApiServiceCollectionExtensions.cs");
        switch (part.ToLowerInvariant())
        {
            case "swagger":
                RemoveLines(extensions, "services.AddSwaggerGen();", "services.AddEndpointsApiExplorer();");
                RemoveBlock(extensions, @"\n        if \(app\.Environment\.IsDevelopment\(\)\)\n        \{\n            app\.UseSwagger\(\);\n            app\.UseSwaggerUI\(\);\n        \}\n");
                RemovePackageReference(Path.Combine(config.SolutionPath, api, $"{config.SolutionName}.Api.csproj"), "Swashbuckle.AspNetCore");
                break;
            case "exception-handler":
                RemoveLines(extensions, "services.AddProblemDetails();", "services.AddExceptionHandler<GlobalExceptionHandler>();", "app.UseExceptionHandler();");
                RemoveLines(extensions, "using " + config.SolutionName + ".Api.Middleware;");
                var handler = Path.Combine(config.SolutionPath, api, "Middleware", "GlobalExceptionHandler.cs");
                if (File.Exists(handler))
                    File.Delete(handler);
                break;
            default:
                throw new ArgumentException($"Unknown replaced part '{part}' in the profile. Use swagger or exception-handler.");
        }
    }

    private static void RemoveLines(string path, params string[] trimmedLines)
    {
        if (!File.Exists(path))
            return;

        var lines = File.ReadAllLines(path).Where(line => !trimmedLines.Contains(line.Trim(), StringComparer.Ordinal)).ToArray();
        File.WriteAllText(path, string.Join("\n", lines) + "\n", new UTF8Encoding(false));
    }

    private static void RemoveBlock(string path, string pattern)
    {
        if (!File.Exists(path))
            return;

        var text = File.ReadAllText(path);
        File.WriteAllText(path, Regex.Replace(text, pattern, "\n", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)), new UTF8Encoding(false));
    }

    private static void RemovePackageReference(string csproj, string id)
    {
        if (!File.Exists(csproj))
            return;

        var text = File.ReadAllText(csproj);
        File.WriteAllText(csproj, Regex.Replace(text, $"\\s*<PackageReference Include=\"{Regex.Escape(id)}\" />", string.Empty, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)), new UTF8Encoding(false));
    }
}
