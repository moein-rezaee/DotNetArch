using DotNetArch.Core.Templating;

namespace DotNetArch.Core.Scaffolding.V2;

/// <summary>
/// <c>new feature</c>: a use-case group without an entity (settings, secrets, bundles ...): the controller and the MCP tools class that typed use cases
/// (<c>new action --input --output</c>) extend through partial classes.
/// </summary>
internal static class FeatureV2Generator
{
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
        writer.Write($"{names.ApiProject}/Controllers/{names.Plural}/{names.Plural}Controller.cs", TemplateRenderer.RenderTemplate("V2/feature/Controller.cs.tpl", tokens));
        if (config.McpEnabled)
            writer.Write($"src/{config.SolutionName}.Mcp/Tools/{names.Plural}/{names.Plural}Tools.cs", TemplateRenderer.RenderTemplate("V2/feature/Tools.cs.tpl", tokens));

        config.Entities[feature] = new EntityStatus { IsFeature = true };
        ConfigManager.Save(config.SolutionPath, config);
        ToolHost.Success($"Feature {feature} generated ({writer.Created.Count} files).", "add use cases with 'new action --entity=" + feature + " --input --output'");
        return true;
    }
}
