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

    /// <summary>
    /// Replaces one kit area by the areas of a profile in an existing solution: the packages of <paramref name="fromArea"/> go (references and central pins), the
    /// line that registered it (<paramref name="oldCall"/>) is replaced, at the same place, by the registration lines of the profile's host, its using is
    /// removed, and the profile's kits are wired as <see cref="Apply"/> does. A second run changes nothing.
    /// </summary>
    public static void Swap(SolutionConfig config, ProfileDefinition profile, string fromArea, string? oldCall, string? oldUsing, IReadOnlyCollection<string>? keep = null)
    {
        Identifier.RequireIdentifier(fromArea, "kit area");
        RemoveAreaPackages(config, fromArea, keep ?? Array.Empty<string>());

        foreach (var host in profile.ExternalKits.SelectMany(k => k.Hosts.Keys).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var project = HostProject(config, host);
            var path = Path.Combine(config.SolutionPath, project, "Program.cs");
            if (!File.Exists(path))
                continue;

            var text = File.ReadAllText(path);
            var newLines = profile.ExternalKits
                .SelectMany(k => k.Hosts.Where(h => h.Key.Equals(host, StringComparison.OrdinalIgnoreCase)).SelectMany(h => h.Value.Builder))
                .Select(l => Token(config, l)).ToList();
            if (!string.IsNullOrWhiteSpace(oldCall) && newLines.Count > 0)
            {
                var line = Regex.Match(text, $@"^(?<indent>[ \t]*)[^\r\n]*\.{Regex.Escape(oldCall)}\([^\r\n]*\);[ \t]*\r?\n", RegexOptions.Multiline | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
                if (line.Success)
                {
                    var nl = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
                    var add = newLines.Where(l => !text.Contains(l, StringComparison.Ordinal)).Select(l => line.Groups["indent"].Value + l + nl);
                    text = text.Remove(line.Index, line.Length).Insert(line.Index, string.Concat(add));
                }
            }

            if (!string.IsNullOrWhiteSpace(oldUsing))
                text = Regex.Replace(text, $@"^using {Regex.Escape(oldUsing)};[ \t]*\r?\n", string.Empty, RegexOptions.Multiline | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            File.WriteAllText(path, text, new UTF8Encoding(false));
        }

        Apply(config, profile);
    }

    private static void RemoveAreaPackages(SolutionConfig config, string area, IReadOnlyCollection<string> keep)
    {
        var prefix = Regex.Escape($"Corevia.Kit.{area}.");
        var reference = new Regex($@"^[ \t]*<Package(?:Reference|Version) Include=""{prefix}[^""]*""[^>]*/>[ \t]*\r?\n", RegexOptions.Multiline | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        var files = Directory.EnumerateFiles(config.SolutionPath, "*.csproj", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(config.SolutionPath, "Directory.Packages.props", SearchOption.TopDirectoryOnly))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            var updated = reference.Replace(text, m => keep.Any(k => m.Value.Contains($"Include=\"{k}\"", StringComparison.Ordinal)) ? m.Value : string.Empty);
            if (updated != text)
                File.WriteAllText(file, updated, new UTF8Encoding(false));
        }
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

    /// <summary>Pins <paramref name="version"/> in the central package file (when the solution has one) and references the package from <paramref name="csproj"/>.</summary>
    public static void EnsurePackage(SolutionConfig config, string csproj, string id, string version)
    {
        var props = Path.Combine(config.SolutionPath, "Directory.Packages.props");
        if (File.Exists(props))
            File.WriteAllText(props, PinVersion(File.ReadAllText(props), id, version), new UTF8Encoding(false));
        AddPackageReference(csproj, id);
    }

    private static string PinVersion(string text, string id, string version)
    {
        var existing = Regex.Match(text, $"<PackageVersion Include=\"{Regex.Escape(id)}\" Version=\"([^\"]*)\" />", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        var line = $"    <PackageVersion Include=\"{id}\" Version=\"{version}\" />\n";
        if (existing.Success)
            return existing.Groups[1].Value == version ? text : text.Replace(existing.Value, line.Trim(), StringComparison.Ordinal);
        return text.Contains("<ItemGroup Label=\"Kits\">", StringComparison.Ordinal)
            ? text.Replace("<ItemGroup Label=\"Kits\">\n", "<ItemGroup Label=\"Kits\">\n" + line, StringComparison.Ordinal)
            : text.Replace("</Project>", $"  <ItemGroup Label=\"Kits\">\n{line}  </ItemGroup>\n\n</Project>", StringComparison.Ordinal);
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
        // A host generated by the tool has markers; a host that was written by hand gets the lines at the place the marker would be.
        text = Place(text, UsingsMarker, wiring.Usings.Select(u => $"using {Token(config, u)};"), @"\A", afterLastUsing: true);
        text = Place(text, BootstrapMarker, wiring.Bootstrap.Select(l => Token(config, l)), @"^[ \t]*(?:var|WebApplicationBuilder)[ \t]+builder[ \t]*=");
        text = Place(text, BuilderMarker, wiring.Builder.Select(l => Token(config, l)), @"^[ \t]*(?:var|WebApplication)[ \t]+app[ \t]*=[ \t]*builder\.Build\(\)");
        text = Place(text, AppMarker, wiring.App.Select(l => Token(config, l)), @"^[ \t]*(?:await[ \t]+)?app\.Run(?:Async)?\(");
        File.WriteAllText(path, text, new UTF8Encoding(false));
    }

    private static string Place(string text, string marker, IEnumerable<string> lines, string fallbackAnchor, bool afterLastUsing = false)
    {
        var wanted = lines.ToList();
        if (wanted.Count == 0)
            return text;
        if (text.Contains(marker, StringComparison.Ordinal))
            return Insert(text, marker, wanted, "");

        var add = wanted.Where(line => !text.Contains(line, StringComparison.Ordinal)).ToList();
        if (add.Count == 0)
            return text;

        var nl = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        if (afterLastUsing)
        {
            var last = Regex.Matches(text, @"^using [^\r\n]+;", RegexOptions.Multiline | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)).LastOrDefault();
            return last is null ? string.Concat(add.Select(line => line + nl)) + text : text.Insert(last.Index + last.Length, string.Concat(add.Select(line => nl + line)));
        }

        var anchor = Regex.Match(text, fallbackAnchor, RegexOptions.Multiline | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        if (!anchor.Success)
            return text;

        var indent = anchor.Value.Length - anchor.Value.TrimStart().Length;
        return text.Insert(anchor.Index, string.Concat(add.Select(line => new string(' ', indent) + line + nl)));
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
            case "database":
                ReplaceDatabase(Path.Combine(config.SolutionPath, $"src/{config.SolutionName}.Infrastructure", "DependencyInjection.cs"));
                break;
            default:
                throw new ArgumentException($"Unknown replaced part '{part}' in the profile. Use swagger, exception-handler or database.");
        }
    }

    /// <summary>
    /// Kit-First for persistence: the template's own connection-string handling and <c>AddDbContext</c> are replaced by the DatabaseConnection Kit
    /// (<c>AddCoreviaDatabase&lt;AppDbContext&gt;</c>), which picks the engine from configuration (<c>Database:Provider</c> or <c>DB_PROVIDER</c>:
    /// Postgres or SqlServer) and builds the connection from <c>POSTGRES_*</c> / <c>SQLSERVER_*</c> values. <c>Database:MigrateOnStartup</c> stays.
    /// </summary>
    private static void ReplaceDatabase(string path)
    {
        if (!File.Exists(path))
            return;
        var text = File.ReadAllText(path);
        if (text.Contains("AddCoreviaDatabase<", StringComparison.Ordinal))
            return;

        text = Regex.Replace(text, @"        services\.AddOptions<DatabaseOptions>\(\).*?\.ValidateOnStart\(\);\n",
            "        services.AddOptions<DatabaseOptions>().Bind(configuration.GetSection(DatabaseOptions.SectionName));\n",
            RegexOptions.Singleline | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        text = Regex.Replace(text, @"        services\.AddDbContext<AppDbContext>\(.*?\n        \}\);\n",
            "        // Kit-First: the DatabaseConnection Kit chooses the engine and builds the connection (no hand-written connection string).\n" +
            "        services.AddCoreviaPostgresProvider().AddCoreviaSqlServerProvider();\n" +
            "        services.AddCoreviaDatabase<AppDbContext>(configuration, options =>\n" +
            "        {\n" +
            "            options.ProviderConfigKeys = new[] { \"Database:Provider\", \"DB_PROVIDER\" };\n" +
            "        });\n",
            RegexOptions.Singleline | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        foreach (var name in new[] { "Corevia.Kit.DatabaseConnection.Core", "Corevia.Kit.DatabaseConnection.Providers.Postgres", "Corevia.Kit.DatabaseConnection.Providers.SqlServer" })
            if (!text.Contains($"using {name};", StringComparison.Ordinal))
                text = $"using {name};\n" + text;
        File.WriteAllText(path, text, new UTF8Encoding(false));
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
