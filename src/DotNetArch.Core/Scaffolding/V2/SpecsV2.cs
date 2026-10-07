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
