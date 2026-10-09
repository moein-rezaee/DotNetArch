using System.Text.RegularExpressions;
using DotNetArch.Core.NetArch;

namespace DotNetArch.Core.Doctor;

/// <summary>Runs the declarative rules of the active profile (profile.yml). The tool knows the rule kinds, never an organisation.</summary>
internal static class ProfileChecks
{
    public static void Run(RepoContext ctx)
    {
        if (ctx.Profile == null)
            return;

        foreach (var rule in ctx.Profile.Rules)
        {
            if (string.IsNullOrWhiteSpace(rule.Id))
                continue;
            var severity = ParseSeverity(rule.Severity);
            switch (rule.Kind)
            {
                case "require-files":
                    var missing = rule.Files.Where(f => f.EndsWith('/') ? !ctx.Files.Any(x => x.StartsWith(f, StringComparison.OrdinalIgnoreCase)) : !ctx.Has(f)).ToList();
                    ctx.Check(rule.Id, "profile", missing.Count == 0, severity, rule.Message, hint: rule.Hint, details: missing);
                    break;
                case "forbid-project-reference":
                    var pattern = Compile(rule.Pattern);
                    var refs = pattern == null ? new List<string>() : ctx.Projects.SelectMany(p => p.ProjectReferences.Where(r => pattern.IsMatch(r)).Select(r => $"{p.Name} -> {r}")).ToList();
                    ctx.Check(rule.Id, "profile", refs.Count == 0, severity, rule.Message, hint: rule.Hint, details: refs);
                    break;
                case "dockerfile-forbid-line":
                    var prefix = rule.Prefix ?? string.Empty;
                    var dockerfiles = ctx.Files.Where(f => Path.GetFileName(f).Equals("Dockerfile", StringComparison.OrdinalIgnoreCase));
                    var offenders = prefix.Length == 0 ? new List<string>() : dockerfiles.Where(d => ctx.Read(d).Split('\n').Any(l => l.TrimStart().StartsWith(prefix, StringComparison.OrdinalIgnoreCase))).ToList();
                    ctx.Check(rule.Id, "profile", offenders.Count == 0, severity, rule.Message, hint: rule.Hint, details: offenders);
                    break;
                case "folder-prefix":
                    var folder = Path.GetFileName(ctx.Root.TrimEnd(Path.DirectorySeparatorChar));
                    ctx.Check(rule.Id, "profile", folder.StartsWith(rule.Prefix ?? string.Empty, StringComparison.Ordinal), severity, rule.Message.Replace("{folder}", folder, StringComparison.Ordinal), hint: rule.Hint);
                    break;
                case "note-if-files-match":
                    var filePattern = Compile(rule.Pattern);
                    var matches = filePattern == null ? new List<string>() : ctx.Files.Where(f => filePattern.IsMatch(f)).ToList();
                    ctx.Check(rule.Id, "profile", matches.Count == 0, DoctorSeverity.Info, rule.Message, hint: rule.Hint, details: matches);
                    break;
                default:
                    ctx.Note(rule.Id, "profile", $"Profile rule '{rule.Id}' has unknown kind '{rule.Kind}' and was skipped.");
                    break;
            }
        }
    }

    internal static DoctorSeverity ParseSeverity(string? value) => value?.ToLowerInvariant() switch
    {
        "error" => DoctorSeverity.Error,
        "info" => DoctorSeverity.Info,
        _ => DoctorSeverity.Warning,
    };

    private static Regex? Compile(string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return null;
        try
        {
            return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
