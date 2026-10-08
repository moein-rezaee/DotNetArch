using System.Text.Json;
using System.Text.RegularExpressions;

namespace DotNetArch.Core.Doctor;

/// <summary>appsettings / .env contract and secret hygiene (DA-C*).</summary>
internal static partial class ConfigChecks
{
    private static readonly JsonDocumentOptions JsonOptions = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

    public static void Run(RepoContext ctx)
    {
        const string cat = "config";
        var hosts = ctx.Projects.Where(p => !p.IsTest && p.Layer is ProjectLayer.Api or ProjectLayer.Mcp).ToList();
        if (hosts.Count == 0)
        {
            ctx.Note("DA-C00", cat, "No Api/Mcp host project found; configuration checks skipped.");
            return;
        }

        var api = hosts.FirstOrDefault(h => h.Layer == ProjectLayer.Api) ?? hosts[0];
        var settings = $"{api.Dir}/appsettings.json";
        ctx.Check("DA-C01", cat, ctx.Has(settings), DoctorSeverity.Warning, $"{api.Name} has no appsettings.json.", settings, "Non-sensitive settings live in appsettings.json.");
        ctx.Check("DA-C02", cat, ctx.Has($"{api.Dir}/appsettings.example.json"), DoctorSeverity.Warning, $"{api.Name} has no appsettings.example.json.", hint: "Generated template that tests keep in sync with the real keys.");
        ctx.Check("DA-C03", cat, ctx.Has($"{api.Dir}/.env.example"), DoctorSeverity.Warning, $"{api.Name} has no .env.example.", hint: "List every secret / run-time key (UPPER_CASE) without real values.");

        var envOnDisk = ctx.Files.Where(f => Path.GetFileName(f).Equals(".env", StringComparison.OrdinalIgnoreCase)).ToList();
        ctx.Check("DA-C04", cat, envOnDisk.Count == 0, DoctorSeverity.Warning, ".env file present in the working tree (make sure it is ignored and never committed).", hint: "Keep only .env.example in git.", details: envOnDisk);

        var leaks = new List<string>();
        foreach (var file in ctx.Files.Where(f => Regex.IsMatch(Path.GetFileName(f), @"^appsettings(\.[\w-]+)?\.json$", RegexOptions.IgnoreCase) && !f.Contains(".example.", StringComparison.OrdinalIgnoreCase)))
            leaks.AddRange(FindSecrets(ctx, file));
        ctx.Check("DA-C05", cat, leaks.Count == 0, DoctorSeverity.Error, "Secret-looking literal values in appsettings files.", hint: "Move to environment / Vault; keep placeholders only.", details: leaks);

        if (ctx.Has(settings) && ctx.Has($"{api.Dir}/appsettings.example.json"))
        {
            var real = Flatten(ctx.Read(settings));
            var example = Flatten(ctx.Read($"{api.Dir}/appsettings.example.json"));
            var absent = real.Except(example, StringComparer.OrdinalIgnoreCase).ToList();
            ctx.Check("DA-C06", cat, absent.Count == 0, DoctorSeverity.Warning, "Keys in appsettings.json that appsettings.example.json does not list.", hint: "Update the example in the same commit as the key change.", details: absent);
        }
    }

    private static IEnumerable<string> FindSecrets(RepoContext ctx, string file)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(ctx.Read(file), JsonOptions);
        }
        catch (JsonException)
        {
            yield break;
        }

        using (doc)
        {
            foreach (var (key, value) in Leaves(doc.RootElement, string.Empty))
            {
                if (IsPlaceholder(value))
                    continue;
                var leaf = key[(key.LastIndexOf(':') + 1)..];
                if (SecretKey().IsMatch(leaf) || ConnectionPassword().IsMatch(value))
                    yield return $"{file}: {key}";
            }
        }
    }

    internal static bool IsPlaceholder(string value)
    {
        var v = value.Trim();
        return v.Length == 0
            || v.StartsWith('<') || v.StartsWith("${", StringComparison.Ordinal) || v.StartsWith("env:", StringComparison.OrdinalIgnoreCase) || v.StartsWith("vault:", StringComparison.OrdinalIgnoreCase) || v.StartsWith("consul:", StringComparison.OrdinalIgnoreCase)
            || v.Contains("example", StringComparison.OrdinalIgnoreCase) || v.Contains("change", StringComparison.OrdinalIgnoreCase) || v.Contains("your", StringComparison.OrdinalIgnoreCase) || v.Contains("placeholder", StringComparison.OrdinalIgnoreCase)
            || v.All(c => c == '*') || v.Equals("true", StringComparison.OrdinalIgnoreCase) || v.Equals("false", StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> Flatten(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json, JsonOptions);
            return Leaves(doc.RootElement, string.Empty).Select(l => l.Key).ToList();
        }
        catch (JsonException)
        {
            return new List<string>();
        }
    }

    private static IEnumerable<(string Key, string Value)> Leaves(JsonElement element, string prefix)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                    foreach (var leaf in Leaves(property.Value, prefix.Length == 0 ? property.Name : prefix + ":" + property.Name))
                        yield return leaf;
                break;
            case JsonValueKind.Array:
                yield return (prefix, string.Empty);
                break;
            default:
                yield return (prefix, element.ValueKind == JsonValueKind.String ? element.GetString() ?? string.Empty : element.ToString());
                break;
        }
    }

    [GeneratedRegex(@"(?i)(password|passwd|secret|apikey|api_key|accesskey|privatekey|clientsecret)$")]
    private static partial Regex SecretKey();

    [GeneratedRegex(@"(?i)(password|pwd)\s*=\s*[^;\s]+")]
    private static partial Regex ConnectionPassword();
}
