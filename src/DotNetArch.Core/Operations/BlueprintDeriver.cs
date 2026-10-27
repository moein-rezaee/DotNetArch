using System.Text;
using System.Text.RegularExpressions;
using DotNetArch.Core.Config;
using DotNetArch.Core.Scaffolding;
using DotNetArch.Core.Scaffolding.V2;

namespace DotNetArch.Core.Operations;

/// <summary>
/// Writes the blueprint of a service that was generated before it had one (DA-BP01): enums, entities with their typed properties, features,
/// use cases and services are read back from <c>dotnet-arch.yml</c> and from the generated code (lengths, nullability, unique and plain indexes,
/// properties left out of the DTO). The result is a starting point to review; the version starts at 1.0.0.
/// </summary>
internal static class BlueprintDeriver
{
    private static readonly string[] Layers = { "Domain.Shared", "Application.Contracts", "HttpApi", "HttpApi.Client" };

    public static string Derive(SolutionConfig config)
    {
        var root = config.SolutionPath;
        var src = Path.Combine(root, "src");
        var text = new StringBuilder();
        text.Append("# Derived from the code and dotnet-arch.yml by `dotnet-arch fix --rules=DA-BP01`; review it, then keep it as the spec of the service.\n");
        text.Append("# schema: the format of this file. version: the version of the model; raise it whenever this file changes.\n");
        text.Append("schema: 1\nversion: 1.0.0\n");

        var enums = Directory.Exists(src)
            ? Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories).Where(f => f.Replace('\\', '/').Contains("/Enums/", StringComparison.Ordinal) && !f.Contains("/obj/", StringComparison.Ordinal)).OrderBy(f => f, StringComparer.Ordinal).ToList()
            : new List<string>();
        var enumLines = new List<string>();
        foreach (var file in enums)
        {
            var match = Regex.Match(File.ReadAllText(file), @"enum\s+(?<name>\w+)\s*\{(?<body>[^}]*)\}", RegexOptions.CultureInvariant | RegexOptions.Singleline, TimeSpan.FromSeconds(2));
            if (!match.Success)
                continue;
            var members = Regex.Matches(match.Groups["body"].Value, @"^\s*(?<m>\w+)\s*(=\s*\d+)?\s*,?\s*$", RegexOptions.CultureInvariant | RegexOptions.Multiline, TimeSpan.FromSeconds(2))
                .Select(m => m.Groups["m"].Value).Where(m => m != "None").ToList();
            enumLines.Add($"  - {{ name: {match.Groups["name"].Value}, members: [{string.Join(", ", members)}] }}");
        }

        Section(text, "enums", enumLines);

        var entityBlocks = new List<string>();
        foreach (var (name, state) in config.Entities.Where(e => !e.Value.IsFeature && !e.Value.HasCrud).OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            var block = Entity(config, src, name);
            if (block is not null)
                entityBlocks.Add(block);
        }

        Section(text, "entities", entityBlocks);
        Section(text, "features", config.Entities.Where(e => e.Value.IsFeature).OrderBy(e => e.Key, StringComparer.Ordinal).Select(e => $"  - {{ name: {e.Key} }}").ToList());

        var useCases = config.UseCases.OrderBy(u => u.Key, StringComparer.Ordinal).Select(u =>
        {
            var useCase = u.Value;
            var line = new StringBuilder($"  - {{ entity: {useCase.Entity}, action: {useCase.Action}, method: {useCase.Method}");
            if (!string.IsNullOrEmpty(useCase.Input))
                line.Append($", input: {Quote(useCase.Input)}");
            if (!string.IsNullOrEmpty(useCase.Output))
                line.Append($", output: {Quote(useCase.Output)}");
            if (useCase.Anonymous)
                line.Append(", anonymous: true");
            return line.Append(" }").ToString();
        }).ToList();
        Section(text, "use_cases", useCases);

        var services = new List<string>();
        if (Directory.Exists(src))
        {
            var folders = config.Entities.Keys.ToDictionary(e => V2Names.For(config, e).Plural, e => e, StringComparer.Ordinal);
            foreach (var file in Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories).Where(f => f.Replace('\\', '/').Contains("/Features/", StringComparison.Ordinal) && f.Replace('\\', '/').Contains("/Services/", StringComparison.Ordinal) && !f.Contains("/obj/", StringComparison.Ordinal)).OrderBy(f => f, StringComparer.Ordinal))
            {
                var content = File.ReadAllText(file);
                var name = Path.GetFileNameWithoutExtension(file);
                if (!Regex.IsMatch(content, $@"public\s+interface\s+I{Regex.Escape(name)}\b", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
                    continue;
                var folder = file.Replace('\\', '/').Split('/') is { } parts ? parts[Array.IndexOf(parts, "Features") + 1] : string.Empty;
                services.Add(folders.TryGetValue(folder, out var entity) ? $"  - {{ name: {name}, entity: {entity} }}" : $"  - {{ name: {name} }}");
            }
        }

        Section(text, "services", services);
        var layers = Layers.Where(l => Directory.Exists(Path.Combine(src, $"{config.SolutionName}.{l}"))).ToList();
        if (layers.Count > 0)
            text.Append($"\nlayers: [{string.Join(", ", layers)}]\n");
        return text.ToString();
    }

    private static string? Entity(SolutionConfig config, string src, string name)
    {
        var domainFile = Directory.Exists(src) ? Directory.EnumerateFiles(src, name + ".cs", SearchOption.AllDirectories).FirstOrDefault(f => f.Replace('\\', '/').Contains("/Entities/", StringComparison.Ordinal) && !f.Contains("/obj/", StringComparison.Ordinal)) : null;
        if (domainFile is null)
            return null;

        var code = File.ReadAllText(domainFile);
        var names = V2Names.For(config, name);
        var description = Regex.Match(code, @"///\s*<summary>(?<t>.*?)</summary>", RegexOptions.CultureInvariant | RegexOptions.Singleline, TimeSpan.FromSeconds(1)).Groups["t"].Value;
        description = Regex.Replace(description, @"\s+", " ").Replace(" Behaviour lives here; state changes only through these methods.", string.Empty, StringComparison.Ordinal).Trim();

        var configuration = Directory.EnumerateFiles(src, name + "Configuration.cs", SearchOption.AllDirectories).FirstOrDefault();
        var configText = configuration is null ? string.Empty : File.ReadAllText(configuration);
        var dto = Directory.EnumerateFiles(src, name + "Dto.cs", SearchOption.AllDirectories).FirstOrDefault();
        var dtoNames = dto is null ? null : Regex.Matches(File.ReadAllText(dto), @"(?<=[\s(,])[\w<>?,\.]+\s+(?<n>\w+)(?=\s*[,)])", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)).Select(m => m.Groups["n"].Value).ToHashSet(StringComparer.Ordinal);

        var properties = new List<string>();
        foreach (Match property in Regex.Matches(code, @"public\s+(?<type>[\w<>?,\.]+)\s+(?<name>\w+)\s*\{\s*get;\s*private\s+set;\s*\}", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2)))
        {
            var propertyName = property.Groups["name"].Value;
            var type = property.Groups["type"].Value;
            var optional = type.EndsWith('?');
            type = type.TrimEnd('?');
            var length = Regex.Match(code, $@"public\s+const\s+int\s+{propertyName}MaxLength\s*=\s*(?<n>\d+);", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            var spec = new StringBuilder(propertyName).Append(':').Append(type);
            if (type == "string" && length.Success)
                spec.Append('(').Append(length.Groups["n"].Value).Append(')');
            if (optional)
                spec.Append('?');
            var index = Regex.Match(configText, $@"HasIndex\(entity\s*=>\s*entity\.{propertyName}\)(?<unique>\.IsUnique\(\))?", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            if (index.Success)
                spec.Append(index.Groups["unique"].Success ? '!' : '#');
            if (dtoNames is not null && !dtoNames.Contains(propertyName))
                spec.Append('~');
            properties.Add(spec.ToString());
        }

        var block = new StringBuilder($"  - name: {name}\n");
        if (description.Length > 0)
            block.Append($"    description: {Quote(description)}\n");
        block.Append($"    properties: {Quote(string.Join(",", properties))}");
        return block.ToString();
    }

    private static void Section(StringBuilder text, string name, IReadOnlyList<string> lines)
    {
        if (lines.Count == 0)
            return;
        text.Append('\n').Append(name).Append(":\n");
        foreach (var line in lines)
            text.Append(line).Append('\n');
    }

    private static string Quote(string value) => "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
}
