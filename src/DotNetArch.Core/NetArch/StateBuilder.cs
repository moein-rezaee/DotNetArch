using System.Text.RegularExpressions;
using DotNetArch.Core.Doctor;

namespace DotNetArch.Core.NetArch;

/// <summary>Derives the project state from the source (the reverse direction of generation). Used by <c>adopt</c>; the source stays untouched.</summary>
internal static partial class StateBuilder
{
    public static ProjectState Build(RepoContext ctx, ProjectState? previous)
    {
        var state = new ProjectState
        {
            Layout = ctx.Layout,
            Mcp = ctx.OfLayer(ProjectLayer.Mcp).Any(),
            Blueprint = previous?.Blueprint ?? NetArchFiles.CurrentBlueprint,
        };

        foreach (var project in ctx.Source.OrderBy(p => p.Name, StringComparer.Ordinal))
            state.Layers.Add(LayerLabel(project));

        foreach (var package in ctx.Projects.SelectMany(p => p.Packages).Select(x => x.Name).Distinct(StringComparer.Ordinal))
        {
            var match = KitPackage().Match(package);
            if (!match.Success)
                continue;
            var area = match.Groups["area"].Value;
            var provider = match.Groups["provider"].Value;
            if (provider.Length == 0)
            {
                state.Kits.TryAdd(area, string.Empty);
                continue;
            }

            state.Kits[area] = state.Kits.TryGetValue(area, out var existing) && existing.Length > 0 ? $"{existing},{provider}" : provider;
        }

        foreach (var application in ctx.OfLayer(ProjectLayer.Application))
        {
            var prefix = application.Dir + "/Features/";
            var slices = ctx.Files
                .Where(f => f.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(f => f[prefix.Length..].Split('/'))
                .Where(parts => parts.Length >= 2)
                .GroupBy(parts => parts[0], StringComparer.Ordinal);
            foreach (var group in slices.OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                var entity = new EntityState();
                entity.Slices.AddRange(group.Select(parts => parts[1]).Where(name => !name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal));
                state.Entities[group.Key] = entity;
            }
        }

        state.Kits = state.Kits.OrderBy(k => k.Key, StringComparer.Ordinal).ToDictionary(k => k.Key, k => k.Value);
        state.Modules["docs"] = ctx.DirectoryExists("docs") ? "on" : "off";
        state.Modules["ci"] = DetectCi(ctx);
        state.Modules["container"] = ctx.Files.Any(f => Path.GetFileName(f).Equals("Dockerfile", StringComparison.OrdinalIgnoreCase)) ? "docker" : "none";
        state.Modules = state.Modules.OrderBy(m => m.Key, StringComparer.Ordinal).ToDictionary(m => m.Key, m => m.Value);
        return state;
    }

    private static string LayerLabel(ProjectInfo project)
    {
        var layer = project.Layer.ToString().ToLowerInvariant();
        if (project.Layer == ProjectLayer.Infrastructure)
        {
            var marker = ".Infrastructure.";
            var index = project.Name.IndexOf(marker, StringComparison.Ordinal);
            if (index >= 0)
                return $"infrastructure:{project.Name[(index + marker.Length)..]}";
        }

        return project.Layer == ProjectLayer.Other ? $"other:{project.Name}" : layer;
    }

    private static string DetectCi(RepoContext ctx)
    {
        if (ctx.Has(".gitlab-ci.yml"))
            return "gitlab";
        if (ctx.Files.Any(f => f.StartsWith(".github/workflows/", StringComparison.OrdinalIgnoreCase)))
            return "github";
        if (ctx.Has("azure-pipelines.yml"))
            return "azure";
        if (ctx.Has("bitbucket-pipelines.yml"))
            return "bitbucket";
        return ctx.Files.Any(f => f.StartsWith(".gitea/workflows/", StringComparison.OrdinalIgnoreCase)) ? "gitea" : "none";
    }

    [GeneratedRegex(@"\.Kit\.(?<area>[A-Za-z0-9]+)\.(?:Abstractions|Core|Providers\.(?<provider>[A-Za-z0-9]+))$")]
    private static partial Regex KitPackage();
}
