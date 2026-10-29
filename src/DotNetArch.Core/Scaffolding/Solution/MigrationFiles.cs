using System.Text;

namespace DotNetArch.Core.Scaffolding.Solution;

/// <summary>
/// Makes the files <c>dotnet ef migrations add</c> writes follow the solution's <c>.editorconfig</c>. EF writes UTF-8 <em>with</em> a byte order mark;
/// the generated solutions ask for plain <c>charset = utf-8</c>, so <c>dotnet format whitespace --verify-no-changes</c> (the lint job) rejected every
/// new migration with CHARSET until someone reformatted it by hand. A solution that asks for <c>utf-8-bom</c> keeps the mark.
/// </summary>
public static class MigrationFiles
{
    private static readonly byte[] Bom = [0xEF, 0xBB, 0xBF];

    /// <summary>Strips the byte order mark from every <c>.cs</c> file in <paramref name="folder"/> unless the editorconfig asks for it. Returns the files it changed.</summary>
    public static IReadOnlyList<string> Normalize(string folder, string? editorConfig)
    {
        var changed = new List<string>();
        if (!Directory.Exists(folder) || (editorConfig?.Contains("utf-8-bom", StringComparison.OrdinalIgnoreCase) ?? false))
            return changed;

        foreach (var file in Directory.EnumerateFiles(folder, "*.cs", SearchOption.TopDirectoryOnly).Order(StringComparer.Ordinal))
        {
            var bytes = File.ReadAllBytes(file);
            if (bytes.Length < Bom.Length || !bytes.AsSpan(0, Bom.Length).SequenceEqual(Bom))
                continue;
            File.WriteAllBytes(file, bytes[Bom.Length..]);
            changed.Add(file);
        }

        return changed;
    }

    /// <summary>The solution's migrations folder, normalised against the <c>.editorconfig</c> at the solution root.</summary>
    public static void NormalizeFor(SolutionConfig config, string infraProject)
    {
        var folder = Path.Combine(config.SolutionPath, Path.GetDirectoryName(infraProject)!, PathConstants.MigrationsRelativePath);
        var editorConfigPath = Path.Combine(config.SolutionPath, ".editorconfig");
        var editorConfig = File.Exists(editorConfigPath) ? File.ReadAllText(editorConfigPath, Encoding.UTF8) : null;
        foreach (var file in Normalize(folder, editorConfig))
            ToolHost.Info($"Migration file follows .editorconfig (no byte order mark): {Path.GetFileName(file)}");
    }
}
