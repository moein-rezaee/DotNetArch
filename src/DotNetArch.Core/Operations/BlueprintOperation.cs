using DotNetArch.Core.Config;
using DotNetArch.Core.Hosting;
using DotNetArch.Core.NetArch;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DotNetArch.Core.Operations;

/// <summary>
/// A blueprint is the declarative description of what a service holds: shared profiles, enums, entities, features, typed use cases, internal services
/// and the layers it ends up in. <c>apply_blueprint</c> runs the same generators as <c>new_enum</c>, <c>new_entity</c>, <c>new_feature</c>,
/// <c>new_action</c>, <c>new_service</c>, <c>apply_profile</c> and <c>add_layer</c>, in that order, in one plan; what is already there is skipped, so the
/// blueprint can be applied again after it grows (R-J9).
/// </summary>
internal sealed class Blueprint
{
    public List<string> Profiles { get; set; } = new();

    public List<BlueprintEnum> Enums { get; set; } = new();

    public List<BlueprintEntity> Entities { get; set; } = new();

    public List<BlueprintFeature> Features { get; set; } = new();

    public List<BlueprintUseCase> UseCases { get; set; } = new();

    public List<BlueprintService> Services { get; set; } = new();

    public List<string> Layers { get; set; } = new();
}

internal sealed class BlueprintEnum
{
    public string? Name { get; set; }

    public string? Entity { get; set; }

    public List<string> Members { get; set; } = new();
}

internal sealed class BlueprintEntity
{
    public string? Name { get; set; }

    public string? Description { get; set; }

    public string? Properties { get; set; }
}

internal sealed class BlueprintFeature
{
    public string? Name { get; set; }

    public string? Description { get; set; }
}

internal sealed class BlueprintUseCase
{
    public string? Entity { get; set; }

    public string? Action { get; set; }

    public string? Method { get; set; }

    public string? Input { get; set; }

    public string? Output { get; set; }

    public bool Anonymous { get; set; }

    public string? Description { get; set; }
}

internal sealed class BlueprintService
{
    public string? Name { get; set; }

    public string? Entity { get; set; }

    public string? Lifetime { get; set; }
}

internal static partial class ScaffoldOperations
{
    private static readonly IDeserializer BlueprintReader = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .Build();

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
            if (!File.Exists(file))
                throw new ArgumentException($"Blueprint file not found: {file}");
            var blueprint = ReadBlueprint(file);
            return InSolution(request, "apply_blueprint", config => ApplyBlueprint(config, blueprint, System.IO.Path.GetDirectoryName(file)!));
        });

    private static Blueprint ReadBlueprint(string file)
    {
        try
        {
            return BlueprintReader.Deserialize<Blueprint>(File.ReadAllText(file)) ?? throw new ArgumentException($"Blueprint is empty: {file}");
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            throw new ArgumentException($"Invalid blueprint {file}: {ex.Message}");
        }
    }

    private static void ApplyBlueprint(SolutionConfig config, Blueprint blueprint, string blueprintFolder)
    {
        var root = config.SolutionPath;
        var src = System.IO.Path.Combine(root, "src");

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
            if (config.UseCases.ContainsKey($"{entity}.{action}"))
                continue;
            ActionStep(config, entity, action, Required(item.Method, $"method of {entity}.{action}"), item.Input, item.Output, item.Anonymous, item.Description);
        }

        foreach (var item in blueprint.Services)
        {
            var name = Required(item.Name, "service name");
            if (!Directory.Exists(src) || !Directory.EnumerateFiles(src, name + ".cs", SearchOption.AllDirectories).Any())
                ServiceStep(config, name, item.Entity, item.Lifetime);
        }

        if (blueprint.Layers.Count == 0)
            return;

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

    private static string Required(string? value, string what) =>
        string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"The blueprint is missing the {what}.") : value.Trim();

    private static bool EnumExists(string src, string name) =>
        Directory.Exists(src) && Directory.EnumerateFiles(src, name + ".cs", SearchOption.AllDirectories).Any(file => file.Replace('\\', '/').Contains("/Enums/", StringComparison.Ordinal));
}
