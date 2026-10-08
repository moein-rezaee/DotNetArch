using System.Text.RegularExpressions;

namespace DotNetArch.Core.Doctor;

/// <summary>Docker, compose, CI and NuGet feed (DA-D*).</summary>
internal static partial class ContainerChecks
{
    private static readonly string[] CiMarkers =
    {
        ".gitlab-ci.yml", "azure-pipelines.yml", "bitbucket-pipelines.yml",
    };

    public static void Run(RepoContext ctx)
    {
        const string cat = "container";
        var dockerfiles = ctx.Files.Where(f => Path.GetFileName(f).Equals("Dockerfile", StringComparison.OrdinalIgnoreCase)).ToList();
        ctx.Check("DA-D01", cat, dockerfiles.Count > 0, DoctorSeverity.Warning, "No Dockerfile.", hint: "dotnet-arch docker add");

        if (dockerfiles.Count > 0)
        {
            var single = dockerfiles.Where(d => ctx.Read(d).Split('\n').Count(l => l.TrimStart().StartsWith("FROM ", StringComparison.OrdinalIgnoreCase)) < 2).ToList();
            ctx.Check("DA-D02", cat, single.Count == 0, DoctorSeverity.Warning, "Dockerfile is not multi-stage.", hint: "Separate restore/build/publish from the runtime image.", details: single);

            var root = dockerfiles.Where(d => !UserLine().IsMatch(ctx.Read(d))).ToList();
            ctx.Check("DA-D03", cat, root.Count == 0, DoctorSeverity.Warning, "Dockerfile runs as root (no USER instruction).", hint: "Add a non-root USER in the final stage.", details: root);

            var hasIgnore = dockerfiles.All(d => ctx.Has(((Path.GetDirectoryName(d) ?? string.Empty).Replace('\\', '/') + "/.dockerignore").TrimStart('/'))) || ctx.Has(".dockerignore");
            ctx.Check("DA-D04", cat, hasIgnore, DoctorSeverity.Error, ".dockerignore is missing (host bin/obj leak into the build context and break restore).", hint: "Ignore bin/, obj/, .git, .env and IDE files.");

            if (ctx.IsCorevia)
            {
                var syntax = dockerfiles.Where(d => ctx.Read(d).Split('\n').Any(l => l.TrimStart().StartsWith("# syntax=", StringComparison.OrdinalIgnoreCase))).ToList();
                ctx.Check("DA-D05", cat, syntax.Count == 0, DoctorSeverity.Error, "Dockerfile pins a '# syntax=' frontend that is unreachable offline / from the private registry.", hint: "Remove the '# syntax=' line.", details: syntax);
            }
        }

        var compose = ctx.Files.Any(f => Regex.IsMatch(Path.GetFileName(f), @"^(docker-)?compose(\.[\w-]+)?\.ya?ml$", RegexOptions.IgnoreCase));
        ctx.Check("DA-D06", cat, compose, DoctorSeverity.Info, "No docker-compose file (fine when deployment is governed elsewhere).");

        var ci = ctx.Files.Any(f => CiMarkers.Contains(f, StringComparer.OrdinalIgnoreCase) || f.StartsWith(".github/workflows/", StringComparison.OrdinalIgnoreCase) || f.StartsWith(".gitea/workflows/", StringComparison.OrdinalIgnoreCase));
        ctx.Check("DA-D07", cat, ci, DoctorSeverity.Warning, "No CI pipeline file.", hint: "dotnet-arch ci add");

        var nuget = ctx.Files.Where(f => Path.GetFileName(f).Equals("NuGet.config", StringComparison.OrdinalIgnoreCase)).ToList();
        var creds = nuget.Where(f => Regex.IsMatch(ctx.Read(f), @"(?i)ClearTextPassword|<add\s+key=""Password""")).ToList();
        ctx.Check("DA-D08", cat, creds.Count == 0, DoctorSeverity.Error, "NuGet.config contains credentials.", hint: "Credentials come from CI secrets or environment, never from files.", details: creds);
    }

    [GeneratedRegex(@"(?im)^\s*USER\s+\S+")]
    private static partial Regex UserLine();
}
