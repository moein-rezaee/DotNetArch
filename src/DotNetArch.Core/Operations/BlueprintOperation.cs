using DotNetArch.Core.Config;
using DotNetArch.Core.Hosting;
using DotNetArch.Core.Scaffolding.V2;
using DotNetArch.Core.NetArch;

namespace DotNetArch.Core.Operations;

/// <summary>
/// A blueprint is the declarative description of what a service holds: shared profiles, enums, entities, features, typed use cases, internal services
/// and the layers it ends up in. <c>apply_blueprint</c> runs the same generators as <c>new_enum</c>, <c>new_entity</c>, <c>new_feature</c>,
/// <c>new_action</c>, <c>new_service</c>, <c>apply_profile</c> and <c>add_layer</c>, in that order, in one plan; what is already there is skipped, so the
/// blueprint can be applied again after it grows (R-J9).
/// </summary>
internal static partial class ScaffoldOperations
{
    private static OperationDefinition ApplyBlueprintOperation() => Mutating(
        "apply_blueprint",
        "Generate what a blueprint file describes (profiles, enums, entities, features, typed use cases, services, layers) in one plan, with the same generators as new_enum, new_entity, new_feature, new_action, new_service, apply_profile and add_layer; what already exists is skipped, so it is safe to repeat. Plan first; writes only with apply.",
        new[]
        {
            PathParam(),
            new OperationParameter("blueprint", "Blueprint file (YAML), for example docs/specs/blueprint.yaml.", Required: true),
            NoMigration(),
        },
        request =>
        {
            var file = System.IO.Path.GetFullPath(request.Get("blueprint")!);
            var blueprint = BlueprintFile.Read(file, out var sha256);
            var version = BlueprintFile.Validate(blueprint, file);
            return InSolution(request, "apply_blueprint", config => ApplyBlueprint(config, blueprint, file, version, sha256));
        });

    private static void ApplyBlueprint(SolutionConfig config, Blueprint blueprint, string file, Version version, string sha256)
    {
        var root = config.SolutionPath;
        var src = System.IO.Path.Combine(root, "src");
        var blueprintFolder = System.IO.Path.GetDirectoryName(file)!;
        var relative = System.IO.Path.GetRelativePath(root, file).Replace('\\', '/');
        BlueprintFile.EnsureVersionDiscipline(BlueprintFile.LoadLock(root), relative, version, sha256);

        foreach (var profile in blueprint.Profiles)
            ApplyProfile(root, System.IO.Path.GetFullPath(profile, blueprintFolder));

        foreach (var item in blueprint.Enums)
        {
            var name = Required(item.Name, "enum name");
            if (EnumExists(src, name))
                continue;
            EnumStep(config, item.Entity, name, item.Members);
        }

        foreach (var item in blueprint.Entities)
        {
            var name = Required(item.Name, "entity name");
            if (!config.Entities.ContainsKey(name))
                EntityStep(config, name, item.Properties, item.Description);
        }

        foreach (var item in blueprint.Features)
        {
            var name = Required(item.Name, "feature name");
            if (!config.Entities.ContainsKey(name))
                FeatureStep(config, name, item.Description);
        }

        foreach (var item in blueprint.UseCases)
        {
            var entity = Required(item.Entity, "use case entity");
            var action = Required(item.Action, $"action of {entity}");
            if (config.UseCases.TryGetValue($"{entity}.{action}", out var recorded))
            {
                // The record keeps the declared lengths (Name:string(120)); a record made before the lengths were kept is brought in line with the blueprint.
                var input = string.Join(",", UseCaseSpec.Parse(item.Input, item.Output).Input.Select(p => $"{p.Name}:{p.RecordedType}"));
                if (!string.Equals(recorded.Input, input, StringComparison.Ordinal))
                {
                    recorded.Input = input;
                    ConfigManager.Save(root, config);
                }

                continue;
            }

            ActionStep(config, entity, action, Required(item.Method, $"method of {entity}.{action}"), item.Input, item.Output, item.Anonymous, item.Description);
        }

        foreach (var item in blueprint.Services)
        {
            var name = Required(item.Name, "service name");
            if (!Directory.Exists(src) || !Directory.EnumerateFiles(src, name + ".cs", SearchOption.AllDirectories).Any())
                ServiceStep(config, name, item.Entity, item.Lifetime);
        }

        if (blueprint.Layers.Count > 0)
        {
            // Layers take what belongs to them from the generated tree, so the tree is normalized first.
            var manual = new List<string>();
            Normalization.Run(root, manual);
            foreach (var layer in blueprint.Layers)
            {
                var result = ProjectOperations.AddLayer.Run(new OperationRequest(new Dictionary<string, string> { ["path"] = root, ["layer"] = layer }, Apply: true));
                if (!result.Ok)
                    ToolHost.Info($"layer {layer}: {result.Error}");
            }
        }

        // The OpenSpec lists what the blueprint generated: entities and features with their routes and operations, and the layout.
        Scaffolding.V2.SpecsV2.SyncOpenSpec(ConfigManager.Load(root) ?? config);

        BlueprintFile.SaveLock(root, new BlueprintLock { File = relative, Schema = blueprint.Schema!.Value, Version = version.ToString(), Sha256 = sha256 });
    }

    private static string Required(string? value, string what) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"The blueprint is missing the {what}.") : value.Trim();

    private static bool EnumExists(string src, string name) =>
        Directory.Exists(src) && Directory.EnumerateFiles(src, name + ".cs", SearchOption.AllDirectories).Any(file => file.Replace('\\', '/').Contains("/Enums/", StringComparison.Ordinal));
}
