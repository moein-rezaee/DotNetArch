namespace DotNetArch.Core.Scaffolding.V2;

/// <summary>Keeps the generated project's specs in step with what was generated (spec-driven development, R-D2).</summary>
internal static class SpecsV2
{
    private const string ContractsMarker = "<!-- dotnet-arch:entities -->";
    private const string OpenSpecMarker = "# <dotnet-arch:entities>";

    public static void AddEntity(SolutionConfig config, string entity)
    {
        var plural = Naming.Pluralize(entity);
        var route = Naming.ToKebabCase(plural);
        InsertBefore(Path.Combine(config.SolutionPath, "docs", "specs", "contracts.md"), ContractsMarker,
            $"| {entity} | `/api/{route}` | list (paged), get, create, update, delete |\n");
        InsertBefore(Path.Combine(config.SolutionPath, "docs", "specs", "openspec.yaml"), OpenSpecMarker,
            $"  - {{name: {entity}, route: /api/{route}, operations: [list, get, create, update, delete]}}\n");
    }

    public static void AddAction(SolutionConfig config, string entity, string action, string httpMethod)
    {
        var route = Naming.ToKebabCase(Naming.Pluralize(entity));
        InsertBefore(Path.Combine(config.SolutionPath, "docs", "specs", "contracts.md"), ContractsMarker,
            $"| {entity} | `{httpMethod.ToUpperInvariant()} /api/{route}/{{id}}/{Naming.ToKebabCase(action)}` | action {action} |\n");
    }

    public static void AddTypedAction(SolutionConfig config, string entity, string action, string httpMethod)
    {
        var route = Naming.ToKebabCase(Naming.Pluralize(entity));
        InsertBefore(Path.Combine(config.SolutionPath, "docs", "specs", "contracts.md"), ContractsMarker,
            $"| {entity} | `{httpMethod.ToUpperInvariant()} /api/{route}/{Naming.ToKebabCase(action)}` | use case {action} |\n");
    }

    public static void AddKit(SolutionConfig config, string area, IReadOnlyList<string> providers)
    {
        var path = Path.Combine(config.SolutionPath, "docs", "specs", "openspec.yaml");
        if (!File.Exists(path))
            return;

        var text = File.ReadAllText(path);
        var entry = $"{{area: {area}, providers: [{string.Join(", ", providers)}]}}";
        if (text.Contains($"area: {area},", StringComparison.Ordinal))
            return;

        text = text.Contains("kits: []", StringComparison.Ordinal)
            ? text.Replace("kits: []", $"kits:\n  - {entry}")
            : text.TrimEnd('\n') + $"\n  - {entry}\n";
        File.WriteAllText(path, text, new System.Text.UTF8Encoding(false));
    }

    /// <summary>
    /// Rewrites the <c>entities:</c> list and the <c>layout:</c> line of <c>docs/specs/openspec.yaml</c> from what the service has generated (the entities,
    /// features and typed use cases of <c>dotnet-arch.yml</c>), so the spec of a service built from a blueprint never says "no entities". Repeating it changes nothing.
    /// </summary>
    public static void SyncOpenSpec(SolutionConfig config)
    {
        var path = Path.Combine(config.SolutionPath, "docs", "specs", "openspec.yaml");
        if (!File.Exists(path))
            return;

        var text = File.ReadAllText(path);
        var marker = text.IndexOf(OpenSpecMarker, StringComparison.Ordinal);
        var header = text.IndexOf("entities:", StringComparison.Ordinal);
        if (marker < 0 || header < 0 || header > marker)
            return;

        var lines = new System.Text.StringBuilder();
        foreach (var (name, status) in config.Entities.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            var route = "/api/" + Naming.ToKebabCase(Naming.Pluralize(name));
            var operations = new List<string>();
            if (status.HasCrud)
                operations.AddRange(new[] { "list", "get", "create", "update", "delete" });
            operations.AddRange(config.UseCases.Values.Where(u => u.Entity == name).Select(u => Naming.ToKebabCase(u.Action)).Order(StringComparer.Ordinal));
            lines.Append($"  - {{name: {name}, route: {route}, operations: [{string.Join(", ", operations)}]}}\n");
        }

        var start = text.IndexOf('\n', header) + 1;
        var markerLine = text.LastIndexOf('\n', marker) + 1;
        text = text[..start] + lines + text[markerLine..];
        if (config.IsV3)
            text = System.Text.RegularExpressions.Regex.Replace(text, @"^layout: v2$", "layout: v3", System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        File.WriteAllText(path, text, new System.Text.UTF8Encoding(false));
    }

    private static void InsertBefore(string path, string marker, string insertion)
    {
        if (!File.Exists(path))
            return;

        var text = File.ReadAllText(path);
        var index = text.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0 || text.Contains(insertion, StringComparison.Ordinal))
            return;

        var lineStart = text.LastIndexOf('\n', index) + 1;
        File.WriteAllText(path, text.Insert(lineStart, insertion), new System.Text.UTF8Encoding(false));
    }
}
