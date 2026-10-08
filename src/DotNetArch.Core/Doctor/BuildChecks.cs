namespace DotNetArch.Core.Doctor;

/// <summary>Repository-level build hygiene (DA-B*).</summary>
internal static class BuildChecks
{
    public static void Run(RepoContext ctx)
    {
        const string cat = "build";
        ctx.Check("DA-B01", cat, ctx.Has("global.json"), DoctorSeverity.Warning, "global.json is missing (SDK version is not pinned).", hint: "Add global.json with the SDK version and rollForward policy.");
        ctx.Check("DA-B02", cat, ctx.Has("Directory.Build.props"), DoctorSeverity.Warning, "Directory.Build.props is missing.", hint: "Centralise Nullable, LangVersion, analyzers and version in one file.");

        var cpm = ctx.Has("Directory.Packages.props");
        ctx.Check("DA-B03", cat, cpm, DoctorSeverity.Warning, "Central package management (Directory.Packages.props) is not used.", hint: "Move PackageReference versions to Directory.Packages.props.");
        if (cpm)
        {
            var inline = ctx.Projects.SelectMany(p => p.Packages.Where(x => x.InlineVersion).Select(x => $"{p.Name}: {x.Name}")).ToList();
            ctx.Check("DA-B04", cat, inline.Count == 0, DoctorSeverity.Warning, "PackageReference with inline Version although central package management is on.", hint: "Remove Version= from the project files.", details: inline);
        }

        ctx.Check("DA-B05", cat, ctx.Has(".editorconfig"), DoctorSeverity.Warning, ".editorconfig is missing.", hint: "Add the shared code-style rules.");

        var props = ctx.Read("Directory.Build.props");
        var nullable = props.Contains("<Nullable>enable</Nullable>", StringComparison.OrdinalIgnoreCase) || (ctx.Source.Any() && ctx.Source.All(p => p.HasNullable));
        ctx.Check("DA-B06", cat, nullable, DoctorSeverity.Warning, "Nullable reference types are not enabled everywhere.", hint: "<Nullable>enable</Nullable> in Directory.Build.props.");

        var twae = props.Contains("TreatWarningsAsErrors", StringComparison.OrdinalIgnoreCase) || ctx.Source.Any(p => p.HasTreatWarningsAsErrors);
        ctx.Check("DA-B07", cat, twae, DoctorSeverity.Warning, "Warnings are not treated as errors in CI/Release.", hint: "Set TreatWarningsAsErrors for CI and Release builds.");

        var gitignore = ctx.Read(".gitignore");
        ctx.Check("DA-B08", cat, ctx.Has(".gitignore"), DoctorSeverity.Error, ".gitignore is missing.", hint: "Ignore bin/, obj/, .env, IDE and OS files.");
        if (ctx.Has(".gitignore"))
        {
            var lines = gitignore.Split('\n').Select(l => l.Trim()).ToHashSet(StringComparer.Ordinal);
            var absent = new[] { "bin/", "obj/", ".env" }.Where(x => !lines.Contains(x) && !lines.Contains("**/" + x) && !lines.Contains("/" + x) && !lines.Contains(x.TrimEnd('/'))).ToList();
            ctx.Check("DA-B09", cat, absent.Count == 0, DoctorSeverity.Error, $".gitignore does not ignore: {string.Join(", ", absent)}.", ".gitignore", "Build output and secrets must never be committed.");
        }
    }
}
