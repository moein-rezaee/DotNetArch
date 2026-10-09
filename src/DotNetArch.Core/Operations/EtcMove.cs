using System.Text.RegularExpressions;
using DotNetArch.Core.Doctor;

namespace DotNetArch.Core.Operations;

/// <summary>
/// Layout v3 keeps non-code files in <c>etc/</c>. A Docker Compose file at the repository root moves to <c>etc/docker/</c>; the relative paths inside it
/// (build context, env files, bind mounts) are re-based so it behaves the same when started with <c>docker compose -f etc/docker/&lt;file&gt;</c>, and
/// mentions of the file name in documentation and pipelines follow. Nothing but paths changes.
/// </summary>
internal static partial class EtcMove
{
    public static bool IsComposeFile(string file) =>
        !file.Contains('/') && ComposeName().IsMatch(file);

    public static IReadOnlyList<string> Pending(RepoContext ctx) => ctx.Files.Where(IsComposeFile).ToList();

    public static IReadOnlyList<FixAction> Plan(RepoContext ctx)
    {
        var moved = Pending(ctx);
        if (moved.Count == 0)
            return Array.Empty<FixAction>();

        var actions = new List<FixAction>();
        foreach (var file in moved)
        {
            var target = "etc/docker/" + file;
            if (ctx.Has(target))
                continue;
            actions.Add(new FixAction(new PlannedChange(target, "move", $"moved from {file}", "DA-A09"), string.Empty, MoveFrom: file));
            var rewritten = Rebase(ctx.Read(file));
            if (rewritten != ctx.Read(file))
                actions.Add(new FixAction(new PlannedChange(target, "modify", "relative paths re-based to the repository root", "DA-A09"), rewritten));
        }

        var names = moved.Select(Path.GetFileName).ToList();
        foreach (var file in ctx.Files)
        {
            if (moved.Contains(file) || !StructuralFixers.IsTextRewriteTarget(file))
                continue;
            var text = ctx.Read(file);
            var updated = text;
            foreach (var name in names)
                updated = Regex.Replace(updated, $@"(?<![\w/.\\-]){Regex.Escape(name!)}", $"etc/docker/{name}", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
            if (updated != text)
                actions.Add(new FixAction(new PlannedChange(file, "modify", "mentions of the compose file follow it to etc/docker/", "DA-A09"), updated));
        }

        return actions;
    }

    /// <summary>Re-bases the relative paths of a compose file that moves two folders down.</summary>
    private static string Rebase(string text)
    {
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith('#'))
                continue;
            line = ContextLine().Replace(line, m => m.Groups["key"].Value + Prefix(m.Groups["value"].Value) + m.Groups["end"].Value);
            line = SourceLine().Replace(line, m => m.Groups["key"].Value + Prefix(m.Groups["value"].Value) + m.Groups["end"].Value);
            line = EnvFileLine().Replace(line, m => m.Groups["key"].Value + Prefix(m.Groups["value"].Value) + m.Groups["end"].Value);
            line = ShortVolume().Replace(line, m => m.Groups["lead"].Value + "../../" + m.Groups["path"].Value + m.Groups["rest"].Value);
            lines[i] = line;
        }

        return string.Join('\n', lines);
    }

    private static string Prefix(string value)
    {
        var quote = value.Length > 0 && value[0] is '"' or '\'' ? value[0].ToString() : string.Empty;
        var bare = value.Trim('"', '\'');
        if (bare.Length == 0 || bare.StartsWith('/') || bare.StartsWith("../", StringComparison.Ordinal) || bare.Contains("://", StringComparison.Ordinal) || bare.StartsWith('$'))
            return value;
        var rebased = bare == "." ? "../.." : "../../" + (bare.StartsWith("./", StringComparison.Ordinal) ? bare[2..] : bare);
        return quote + rebased + quote;
    }

    [GeneratedRegex(@"^docker-compose(?:\.[\w-]+)*\.ya?ml$|^compose(?:\.[\w-]+)*\.ya?ml$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ComposeName();

    [GeneratedRegex(@"(?<key>^\s*context:\s*)(?<value>\S+)(?<end>\s*$)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ContextLine();

    [GeneratedRegex(@"(?<key>^\s*source:\s*)(?<value>\S+)(?<end>\s*$)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SourceLine();

    [GeneratedRegex(@"(?<key>^\s*(?:env_file:\s*|-\s+(?=[\w./\-]*\.env\b)))(?<value>\S+)(?<end>\s*$)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex EnvFileLine();

    [GeneratedRegex(@"(?<lead>^\s*-\s+[""']?)\./(?<path>[^:\s""']+)(?<rest>:.*$)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ShortVolume();
}
