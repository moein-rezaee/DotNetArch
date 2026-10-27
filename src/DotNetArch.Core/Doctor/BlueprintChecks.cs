using DotNetArch.Core.Config;
using DotNetArch.Core.Operations;

namespace DotNetArch.Core.Doctor;

/// <summary>
/// The service blueprint (DA-BP*): it exists, reads, carries a schema and a version, matches what was generated, and was applied at its current
/// version. The tool does not depend on any standards repository: the blueprint is a file of the service.
/// </summary>
internal static class BlueprintChecks
{
    public static void Run(RepoContext ctx)
    {
        const string cat = "blueprint";
        if (!ctx.Has("dotnet-arch.yml"))
            return;

        var config = ConfigManager.Load(ctx.Root);
        if (config is null || !config.IsV2)
            return;

        var file = BlueprintFile.FindFile(ctx.Root);
        if (file is null)
        {
            ctx.Check("DA-BP01", cat, false, DoctorSeverity.Warning, "The service has no blueprint (docs/specs/blueprint.yaml).", hint: "Describe the model in a blueprint (schema, version, enums, entities, features, use cases) and generate with apply_blueprint, so the model is a reviewed, versioned file.");
            return;
        }

        var relative = Path.GetRelativePath(ctx.Root, file).Replace('\\', '/');
        Blueprint blueprint;
        string sha256;
        Version version;
        try
        {
            blueprint = BlueprintFile.Read(file, out sha256);
            version = BlueprintFile.Validate(blueprint, relative);
        }
        catch (ArgumentException ex)
        {
            ctx.Check("DA-BP02", cat, false, DoctorSeverity.Error, ex.Message, path: relative, hint: "A blueprint needs 'schema: 1' and 'version: x.y.z' and must be valid YAML.");
            return;
        }

        ctx.Check("DA-BP02", cat, true, DoctorSeverity.Error, "The blueprint reads.");

        var src = Path.Combine(ctx.Root, "src");
        bool Generated(string name) => Directory.Exists(src) && Directory.EnumerateFiles(src, name + ".cs", SearchOption.AllDirectories).Any();
        var missing = new List<string>();
        missing.AddRange(blueprint.Enums.Where(e => !string.IsNullOrWhiteSpace(e.Name) && !Generated(e.Name!)).Select(e => $"enum {e.Name}"));
        missing.AddRange(blueprint.Entities.Where(e => !string.IsNullOrWhiteSpace(e.Name) && !config.Entities.ContainsKey(e.Name!)).Select(e => $"entity {e.Name}"));
        missing.AddRange(blueprint.Features.Where(f => !string.IsNullOrWhiteSpace(f.Name) && !config.Entities.ContainsKey(f.Name!)).Select(f => $"feature {f.Name}"));
        missing.AddRange(blueprint.UseCases.Where(u => !config.UseCases.ContainsKey($"{u.Entity}.{u.Action}")).Select(u => $"use case {u.Entity}.{u.Action}"));
        missing.AddRange(blueprint.Services.Where(s => !string.IsNullOrWhiteSpace(s.Name) && !Generated(s.Name!)).Select(s => $"service {s.Name}"));
        ctx.Check("DA-BP03", cat, missing.Count == 0, DoctorSeverity.Warning, "The blueprint describes things that were not generated.", path: relative, hint: "dotnet-arch fix --rules=DA-BP03 --apply (runs apply_blueprint).", details: missing);

        var inBlueprint = blueprint.Entities.Select(e => e.Name).Concat(blueprint.Features.Select(f => f.Name)).Where(n => n != null).ToHashSet(StringComparer.Ordinal);
        var useCases = blueprint.UseCases.Select(u => $"{u.Entity}.{u.Action}").ToHashSet(StringComparer.Ordinal);
        var extra = config.Entities.Keys.Where(name => !inBlueprint.Contains(name)).Select(name => $"entity {name}")
            .Concat(config.UseCases.Keys.Where(key => !useCases.Contains(key)).Select(key => $"use case {key}"))
            .ToList();
        ctx.Check("DA-BP04", cat, extra.Count == 0, DoctorSeverity.Warning, "The service has entities or use cases that the blueprint does not describe.", path: relative, hint: "Add them to the blueprint (and raise its version), so the spec stays the source of the model.", details: extra);

        var applied = BlueprintFile.LoadLock(ctx.Root);
        if (applied is null)
        {
            ctx.Check("DA-BP05", cat, false, DoctorSeverity.Warning, "The blueprint was never applied by apply_blueprint (no record of an applied version).", path: relative, hint: "dotnet-arch apply_blueprint --path . --blueprint " + relative + " --apply");
            return;
        }

        var current = string.Equals(applied.Sha256, sha256, StringComparison.Ordinal) && string.Equals(applied.Version, version.ToString(), StringComparison.Ordinal);
        string message;
        if (current)
            message = string.Empty;
        else if (string.Equals(applied.Version, version.ToString(), StringComparison.Ordinal))
            message = $"The blueprint changed but its version is still {version}; raise 'version', then apply it.";
        else
            message = $"The blueprint is at version {version} but version {applied.Version} was applied; apply it.";
        ctx.Check("DA-BP05", cat, current, DoctorSeverity.Warning, message, path: relative, hint: "dotnet-arch fix --rules=DA-BP05 --apply (runs apply_blueprint).");
    }
}
