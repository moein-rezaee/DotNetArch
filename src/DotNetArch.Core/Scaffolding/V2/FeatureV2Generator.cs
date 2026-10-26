using DotNetArch.Core.Templating;

namespace DotNetArch.Core.Scaffolding.V2;

/// <summary>
/// <c>new feature</c>: a use-case group without an entity (settings, secrets, bundles ...): the controller and the MCP tools class that typed use cases
/// (<c>new action --input --output</c>) extend through partial classes.
/// </summary>
internal static class FeatureV2Generator
{
    /// <summary>
    /// A typed use case on an entity that has no CRUD (a model from <c>new entity</c>) needs the partial controller and, with an MCP host, the partial
    /// tools class to extend. They are created when missing and never overwritten (a CRUD slice generated earlier already has them).
    /// </summary>
    public static void EnsureSkeleton(SolutionConfig config, string entity)
    {
        var names = V2Names.For(config, entity);
        var tokens = names.Tokens();
        tokens["Summary"] = $"{entity} use cases.";
        var writer = new FileWriter(config.SolutionPath);
        WriteController(config, writer, names, tokens);
        EnsureTools(config, entity);
    }

    /// <summary>
    /// The partial controller the use cases extend. Once the controllers live in the HttpApi layer, a second declaration in the Api project would be a
    /// duplicate class on the same route, so nothing is written there.
    /// </summary>
    private static void WriteController(SolutionConfig config, FileWriter writer, V2Names names, Dictionary<string, string> tokens)
    {
        var inHttpApi = Path.Combine(config.SolutionPath, "src", $"{config.SolutionName}.HttpApi", "Controllers", names.Plural, $"{names.Plural}Controller.cs");
        if (File.Exists(inHttpApi))
            return;

        writer.Write($"{names.ApiProject}/Controllers/{names.Plural}/{names.Plural}Controller.cs", TemplateRenderer.RenderTemplate("V2/feature/Controller.cs.tpl", tokens));
    }

    public static void EnsureTools(SolutionConfig config, string entity)
    {
        if (!config.McpEnabled)
            return;

        var names = V2Names.For(config, entity);
        var tokens = names.Tokens();
        new FileWriter(config.SolutionPath).Write($"src/{config.SolutionName}.Mcp/Tools/{names.Plural}/{names.Plural}Tools.cs", TemplateRenderer.RenderTemplate("V2/feature/Tools.cs.tpl", tokens));
    }

    public static bool Generate(SolutionConfig config, string feature, string? description)
    {
        Identifier.RequireIdentifier(feature, "feature name");
        if (config.ApiStyle.Equals("fast", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("new_feature needs --style=controller.");
        if (config.Entities.TryGetValue(feature, out var existing) && (existing.HasCrud || existing.HasModel || existing.IsFeature))
        {
            ToolHost.Error($"'{feature}' already exists as an entity or a feature.");
            return false;
        }

        var names = V2Names.For(config, feature);
        var tokens = names.Tokens();
        tokens["Summary"] = string.IsNullOrWhiteSpace(description)
            ? $"{feature} use cases."
            : System.Text.RegularExpressions.Regex.Replace(description.Trim(), "[<>&\"\\r\\n]", " ");

        var writer = new FileWriter(config.SolutionPath);
        WriteController(config, writer, names, tokens);
        if (config.McpEnabled)
            writer.Write($"src/{config.SolutionName}.Mcp/Tools/{names.Plural}/{names.Plural}Tools.cs", TemplateRenderer.RenderTemplate("V2/feature/Tools.cs.tpl", tokens));

        config.Entities[feature] = new EntityStatus { IsFeature = true };
        ConfigManager.Save(config.SolutionPath, config);
        ToolHost.Success($"Feature {feature} generated ({writer.Created.Count} files).", "add use cases with 'new action --entity=" + feature + " --input --output'");
        return true;
    }
}
