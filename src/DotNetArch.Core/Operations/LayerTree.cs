using System.Text.RegularExpressions;
using DotNetArch.Core.Doctor;

namespace DotNetArch.Core.Operations;

/// <summary>
/// Folder tree inside the layer projects (DA-A10). Every source file sits in a folder named for what it is or for the feature it belongs to,
/// all the way down: no loose files at a project root (but the composition files), and no folder that dumps more than <see cref="FlatLimit"/>
/// files of several features side by side. Feature names come from the repository itself (the folders under <c>Features/</c> and
/// <c>Contracts/</c> of Application and Application.Contracts). Files only move, inside their project; namespaces and code stay as they are.
/// </summary>
internal static class LayerTree
{
    public const int FlatLimit = 12;

    private static readonly HashSet<string> RootAllowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "Program.cs", "AssemblyMarker.cs", "DependencyInjection.cs", "GlobalUsings.cs", "AssemblyInfo.cs",
    };

    // file-name suffix -> kind folder (first match wins)
    private static readonly (string Suffix, string Folder)[] Kinds =
    {
        ("Repository", "Repositories"), ("Options", "Options"), ("Client", "Clients"), ("Mapper", "Mappers"), ("Controller", "Controllers"),
        ("Handler", "Handlers"), ("Validator", "Validators"), ("Extensions", "Extensions"), ("Job", "Jobs"), ("Service", "Services"),
        ("Dto", "Dtos"), ("Entity", "Entities"), ("Resolver", "Resolvers"),
    };

    private static readonly HashSet<string> SkippedFolders = new(StringComparer.OrdinalIgnoreCase) { "Migrations", "Properties", "wwwroot" };

    public sealed record TreeMove(string From, string To, string Reason);

    /// <summary>Feature folder names known to the repository (plural, as they appear under Features/ and Contracts/).</summary>
    public static IReadOnlyList<string> Features(RepoContext ctx)
    {
        var result = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var project in ctx.Source.Where(p => p.Layer is ProjectLayer.Application or ProjectLayer.ApplicationContracts))
        {
            var prefix = project.Dir + "/";
            foreach (var file in ctx.Files.Where(f => f.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
            {
                var parts = file[prefix.Length..].Split('/');
                if (parts.Length >= 3 && parts[0] is "Features" or "Contracts" && parts[1] is not ("Common" or "Shared" or "Pagination"))
                    result.Add(parts[1]);
            }
        }

        return result.ToList();
    }

    /// <summary>The feature a type or file belongs to ("IProductRepository" and "ProductClient" belong to "Products"), or null.</summary>
    public static string? FeatureFor(IReadOnlyList<string> features, string stem)
    {
        if (stem.Length > 1 && stem[0] == 'I' && char.IsUpper(stem[1]))
            stem = stem[1..];
        string? best = null;
        var bestLength = 0;
        foreach (var feature in features)
        {
            var singular = Singular(feature);
            if (singular.Length <= bestLength)
                continue;
            for (var at = stem.IndexOf(singular, StringComparison.Ordinal); at >= 0; at = stem.IndexOf(singular, at + 1, StringComparison.Ordinal))
            {
                var startsWord = at == 0 || char.IsLower(stem[at - 1]);
                var endsWord = at + singular.Length == stem.Length || char.IsUpper(stem[at + singular.Length]);
                if (!startsWord || !endsWord)
                    continue;
                best = feature;
                bestLength = singular.Length;
                break;
            }
        }

        return best;
    }

    /// <summary>Folder of a generated typed client: the feature it serves ("ProductClient" goes to "Products/").</summary>
    public static string ClientFolder(IReadOnlyList<string> features, string clientName)
    {
        var trimmed = Regex.Replace(clientName, "Client$", string.Empty, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        return FeatureFor(features, trimmed) ?? (trimmed.Length > 0 ? trimmed : "Common");
    }

    private static string Singular(string plural) =>
        plural.EndsWith("ies", StringComparison.Ordinal) ? plural[..^3] + "y"
        : plural.EndsWith('s') && !plural.EndsWith("ss", StringComparison.Ordinal) ? plural[..^1]
        : plural;

    public static IReadOnlyList<TreeMove> Moves(RepoContext ctx)
    {
        var features = Features(ctx);
        var moves = new List<TreeMove>();
        var taken = new HashSet<string>(ctx.Files, StringComparer.OrdinalIgnoreCase);

        foreach (var project in ctx.Source)
        {
            var prefix = project.Dir + "/";
            var files = ctx.Files
                .Where(f => f.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                            && !f.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase) && !f.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase)
                            && !f[prefix.Length..].Split('/').SkipLast(1).Any(s => SkippedFolders.Contains(s)))
                .ToList();
            var csproj = ctx.Read(project.File);

            foreach (var file in files.Where(f => !f[prefix.Length..].Contains('/')))
            {
                var name = Path.GetFileName(file);
                if (RootAllowed.Contains(name) || name.EndsWith("Module.cs", StringComparison.Ordinal))
                    continue;
                var folder = RootFolder(project, Path.GetFileNameWithoutExtension(name), features);
                Add(moves, taken, csproj, project, file, $"{prefix}{folder}/{name}", "loose file at the project root");
            }

            foreach (var group in files.Where(f => f[prefix.Length..].Contains('/')).GroupBy(f => f[..f.LastIndexOf('/')], StringComparer.OrdinalIgnoreCase))
            {
                if (group.Count() <= FlatLimit)
                    continue;
                var directory = group.Key;
                var stems = group.ToDictionary(f => f, f => Path.GetFileNameWithoutExtension(f));
                var firstWords = stems.Values.Select(FirstWord).GroupBy(w => w).Where(g => g.Count() >= 2).Select(g => g.Key).ToHashSet(StringComparer.Ordinal);
                foreach (var file in group)
                {
                    string? key = FeatureFor(features, stems[file]);
                    if (key == null)
                    {
                        var word = FirstWord(stems[file]);
                        key = word.Length >= 4 && firstWords.Contains(word) ? word : null;
                    }

                    key ??= Regex.Replace(stems[file], "(Entity|Repository|Service|Job|Dto|Options|Mapper|Resolver)$", string.Empty, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
                    if (key.Length == 0 || key == stems[file] && FeatureFor(features, stems[file]) == null)
                        continue;
                    if (directory[prefix.Length..].Split('/').Contains(key, StringComparer.OrdinalIgnoreCase))
                        continue;
                    Add(moves, taken, csproj, project, file, $"{directory}/{key}/{Path.GetFileName(file)}", $"folder holds more than {FlatLimit} files side by side");
                }
            }
        }

        return moves;
    }

    public static IReadOnlyList<FixAction> Plan(RepoContext ctx) =>
        Moves(ctx).Select(m => new FixAction(new PlannedChange(m.To, "move", $"moved from {m.From} ({m.Reason})", "DA-A10"), string.Empty, MoveFrom: m.From)).ToList();

    private static void Add(List<TreeMove> moves, HashSet<string> taken, string csproj, ProjectInfo project, string from, string to, string reason)
    {
        if (taken.Contains(to))
            return;
        var relative = from[(project.Dir.Length + 1)..];
        if (csproj.Contains(relative, StringComparison.OrdinalIgnoreCase) || csproj.Contains(relative.Replace('/', '\\'), StringComparison.OrdinalIgnoreCase))
            return;
        taken.Add(to);
        moves.Add(new TreeMove(from, to, reason));
    }

    private static string RootFolder(ProjectInfo project, string stem, IReadOnlyList<string> features)
    {
        if (project.Layer != ProjectLayer.HttpApiClient)
        {
            foreach (var (suffix, folder) in Kinds)
            {
                if (stem.EndsWith(suffix, StringComparison.Ordinal))
                    return folder;
            }
        }

        var trimmed = Regex.Replace(stem, "(Client|Controller|Options|Service)$", string.Empty, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        return FeatureFor(features, trimmed) ?? (project.Layer == ProjectLayer.HttpApiClient && trimmed.Length > 0 && !trimmed.StartsWith("Api", StringComparison.Ordinal) ? trimmed : "Common");
    }

    private static string FirstWord(string stem)
    {
        if (stem.Length > 1 && stem[0] == 'I' && char.IsUpper(stem[1]))
            stem = stem[1..];
        var match = Regex.Match(stem, "^[A-Z][a-z0-9]*", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        return match.Success ? match.Value : stem;
    }
}
