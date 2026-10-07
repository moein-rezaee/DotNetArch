using DotNetArch.Core.Templating;

namespace DotNetArch.Core.Scaffolding.V2;

/// <summary>Adds a custom use case (<c>new action</c>) to an existing entity in a v2 solution.</summary>
internal static class ActionV2Generator
{
    public static bool Generate(SolutionConfig config, string entity, string actionName, string httpMethod)
    {
        Identifier.RequireIdentifier(entity, "entity name");
        Identifier.RequireIdentifier(actionName, "action name");

        var names = V2Names.For(config, entity);
        if (!EntityGeneration.EntityExists(config, entity))
        {
            ToolHost.Error($"Entity '{entity}' does not exist.", "Create it first with 'new crud'.");
            return false;
        }

        var isQuery = httpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase);
        var kind = isQuery ? "Query" : "Command";
        var folder = $"{names.FeatureFolder}/Actions/{actionName}{entity}";
        var outputs = new List<(string Template, string Output)>();
        if (isQuery)
        {
            outputs.Add(("Query", $"{folder}/{actionName}{entity}Query.cs"));
            outputs.Add(("QueryHandler", $"{folder}/{actionName}{entity}QueryHandler.cs"));
        }
        else
        {
            outputs.Add(("DomainPartial", $"{names.DomainProject}/Entities/{entity}.{actionName}.cs"));
            outputs.Add(("Command", $"{folder}/{actionName}{entity}Command.cs"));
            outputs.Add(("CommandHandler", $"{folder}/{actionName}{entity}CommandHandler.cs"));
            outputs.Add(("CommandValidator", $"{folder}/{actionName}{entity}CommandValidator.cs"));
        }
        var minimalApi = config.ApiStyle.Equals("fast", StringComparison.OrdinalIgnoreCase);
        outputs.Add(minimalApi
            ? ("Endpoint", $"{names.ApiProject}/Endpoints/{names.Plural}/{actionName}{entity}Endpoint.cs")
            : ("ControllerPartial", $"{names.ApiProject}/Controllers/{names.Plural}/{names.Plural}Controller.{actionName}.cs"));

        var tokens = names.Tokens();
        tokens["ActionName"] = actionName;
        tokens["ActionRoute"] = Naming.ToKebabCase(actionName);
        tokens["HttpVerb"] = char.ToUpperInvariant(httpMethod[0]) + httpMethod[1..].ToLowerInvariant();
        tokens["RequestKind"] = kind;

        if (new FileWriter(config.SolutionPath).Exists(outputs[^1].Output))
        {
            ToolHost.Error("Action with the same name already exists for this entity.");
            return false;
        }

        var writer = new FileWriter(config.SolutionPath);
        foreach (var (template, output) in outputs)
            writer.Write(output, TemplateRenderer.RenderTemplate($"V2/action/{template}.cs.tpl", tokens));

        var state = config.Entities.TryGetValue(entity, out var existing) ? existing : new EntityStatus();
        state.HasAction = true;
        config.Entities[entity] = state;
        ConfigManager.Save(config.SolutionPath, config);

        if (config.McpEnabled)
            McpV2Generator.AddActionTool(config, entity, actionName, isQuery);

        SpecsV2.AddAction(config, entity, actionName, httpMethod);

        ToolHost.Success($"Action {actionName} for {entity} generated ({writer.Created.Count} files).");
        return true;
    }
}
