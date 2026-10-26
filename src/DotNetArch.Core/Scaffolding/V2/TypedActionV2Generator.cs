using System.Text;
using System.Text.RegularExpressions;
using DotNetArch.Core.Templating;

namespace DotNetArch.Core.Scaffolding.V2;

/// <summary>
/// A typed use case (<c>new action --input=... --output=...</c>): a request with its own properties and its own result, not tied to an id. It generates the
/// request record, the result record, a handler stub (the business logic is written by hand), the validator, the controller action and, with an MCP host,
/// the tool. The typed client and the other hosts follow the controller through the fixers and the recorded <see cref="UseCaseState"/>.
/// </summary>
internal static class TypedActionV2Generator
{
    public static bool Generate(SolutionConfig config, string entity, string actionName, string httpMethod, UseCaseSpec spec, bool anonymous, string? description)
    {
        Identifier.RequireIdentifier(entity, "entity name");
        Identifier.RequireIdentifier(actionName, "action name");
        if (config.ApiStyle.Equals("fast", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Typed actions need --style=controller (a controller action is generated).");
        if (!EntityGeneration.EntityExists(config, entity))
        {
            ToolHost.Error($"Entity '{entity}' does not exist.", "Create it first with 'new crud'.");
            return false;
        }

        if (EntityGeneration.IsFeature(config, entity) && spec.Output.Kind == UseCaseOutputKind.EntityDto)
            throw new ArgumentException($"'{entity}' is a feature without an entity DTO: give the use case an explicit --output.");

        var names = V2Names.For(config, entity);
        var tokens = Tokens(config, entity, actionName, httpMethod, spec, anonymous, description);
        var kind = tokens["RequestKind"];
        var folder = $"{names.FeatureFolder}/Actions/{actionName}{entity}";
        var controller = $"{names.ApiProject}/Controllers/{names.Plural}/{names.Plural}Controller.{actionName}.cs";
        var writer = new FileWriter(config.SolutionPath);
        var key = $"{entity}.{actionName}";
        if (config.UseCases.TryGetValue(key, out var recorded))
        {
            // The use case exists (its files may have moved to other projects): repeat only the parts that follow it, without overwriting anything.
            FeatureV2Generator.EnsureSkeleton(config, entity);
            if (config.McpEnabled)
                McpV2Generator.AddTypedActionTool(config, recorded);
            if (config.CliEnabled)
                CliV2Generator.AddTypedActionCommand(config, recorded);
            ToolHost.Info($"Use case {actionName} for {entity} already exists; its tool and command were checked.");
            return true;
        }

        if (writer.Exists(controller))
        {
            ToolHost.Error("Action with the same name already exists for this entity.");
            return false;
        }

        FeatureV2Generator.EnsureSkeleton(config, entity);
        writer.Write($"{folder}/{actionName}{entity}{kind}.cs", TemplateRenderer.RenderTemplate("V2/action-typed/Command.cs.tpl", tokens));
        if (spec.Output.Kind == UseCaseOutputKind.Generated)
            writer.Write($"{folder}/{actionName}{entity}Result.cs", TemplateRenderer.RenderTemplate("V2/action-typed/Result.cs.tpl", tokens));
        writer.Write($"{folder}/{actionName}{entity}{kind}Handler.cs", TemplateRenderer.RenderTemplate("V2/action-typed/Handler.cs.tpl", tokens));
        writer.Write($"{folder}/{actionName}{entity}{kind}Validator.cs", TemplateRenderer.RenderTemplate("V2/action-typed/Validator.cs.tpl", tokens));
        writer.Write(controller, TemplateRenderer.RenderTemplate("V2/action-typed/ControllerPartial.cs.tpl", tokens));

        var state = config.Entities.TryGetValue(entity, out var existing) ? existing : new EntityStatus();
        state.HasAction = true;
        config.Entities[entity] = state;
        var useCase = new UseCaseState
        {
            Entity = entity,
            Action = actionName,
            Method = httpMethod.ToUpperInvariant(),
            Anonymous = anonymous,
            Input = string.Join(",", spec.Input.Select(p => $"{p.Name}:{p.Type}")),
            Output = OutputText(spec.Output),
        };
        config.UseCases[$"{entity}.{actionName}"] = useCase;
        ConfigManager.Save(config.SolutionPath, config);

        if (config.McpEnabled)
            McpV2Generator.AddTypedActionTool(config, useCase);
        if (config.CliEnabled)
            CliV2Generator.AddTypedActionCommand(config, useCase);

        SpecsV2.AddTypedAction(config, entity, actionName, httpMethod);
        ToolHost.Success($"Use case {actionName} for {entity} generated ({writer.Created.Count} files).", "implement the handler: the generated body throws NotImplementedException");
        return true;
    }

    /// <summary>Rebuilds the spec of a recorded use case (used by the MCP and CLI generators).</summary>
    internal static UseCaseSpec SpecOf(UseCaseState state) => UseCaseSpec.Parse(state.Input, string.IsNullOrEmpty(state.Output) ? "none" : state.Output);

    internal static Dictionary<string, string> Tokens(SolutionConfig config, string entity, string actionName, string httpMethod, UseCaseSpec spec, bool anonymous, string? description)
    {
        var names = V2Names.For(config, entity);
        var tokens = names.Tokens();
        var isQuery = httpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase);
        var request = $"{actionName}{entity}{(isQuery ? "Query" : "Command")}";
        var hasResult = spec.Output.Kind != UseCaseOutputKind.None;
        var resultType = spec.Output.Kind switch
        {
            UseCaseOutputKind.EntityDto => $"{entity}Dto",
            UseCaseOutputKind.Generated => $"{actionName}{entity}Result",
            UseCaseOutputKind.Existing => spec.Output.Type,
            _ => string.Empty,
        };
        var summary = Summary(description, actionName, entity);

        tokens["ActionName"] = actionName;
        tokens["ActionRoute"] = Naming.ToKebabCase(actionName);
        tokens["ActionSnake"] = Naming.ToKebabCase(actionName).Replace('-', '_');
        tokens["ToolName"] = Naming.ToKebabCase(entity).Replace('-', '_');
        tokens["EntityLower"] = entity.ToLowerInvariant();
        tokens["HttpVerb"] = char.ToUpperInvariant(httpMethod[0]) + httpMethod[1..].ToLowerInvariant();
        tokens["RequestKind"] = isQuery ? "Query" : "Command";
        tokens["IsReadOnly"] = isQuery ? "true" : "false";
        tokens["Summary"] = summary;
        tokens["ResultType"] = resultType;
        tokens["ResultGeneric"] = hasResult ? $"<{resultType}>" : string.Empty;
        tokens["ResultGenericComma"] = hasResult ? $", {resultType}" : string.Empty;
        tokens["TaskGeneric"] = hasResult ? $"<{resultType}>" : string.Empty;
        tokens["RecordParameters"] = spec.RecordParameters();
        tokens["ResultParameters"] = spec.Output.Kind == UseCaseOutputKind.Generated ? string.Join(", ", spec.Output.Properties.OrderBy(p => p.IsOptional).Select(p => p.Declaration)) : string.Empty;
        tokens["ValidatorRules"] = spec.ValidatorRules();
        var (needsDtos, needsEnums) = ClassifyTypes(config, spec);
        tokens["DtoUsing"] = (needsDtos ? $"using {config.SolutionName}.Application.Features.{names.Plural}.Dtos;\n" : string.Empty)
                             + (needsEnums ? $"using {config.SolutionName}.Domain.Enums;\n" : string.Empty);
        tokens["AnonymousAttribute"] = anonymous ? "\n    [AllowAnonymous]" : string.Empty;
        tokens["ControllerBody"] = ControllerBody(actionName, request, resultType, hasResult, spec, isQuery);

        var parameters = new List<string>();
        if (spec.Input.Count > 0)
            parameters.Add(spec.MethodParameters(p => $"[Description(\"The {p.Description}.\")] "));
        parameters.Add("CancellationToken cancellationToken = default");
        tokens["McpParameters"] = string.Join(", ", parameters);
        tokens["NamedArguments"] = spec.NamedArguments();
        tokens["McpReturn"] = hasResult ? $"Task<{resultType}>" : "Task<string>";
        tokens["McpSuccess"] = hasResult ? string.Empty : ", \"Done.\"";
        return tokens;
    }

    private static string ControllerBody(string actionName, string request, string resultType, bool hasResult, UseCaseSpec spec, bool isQuery)
    {
        var binding = isQuery ? "[FromQuery]" : "[FromBody]";
        var parameter = spec.Input.Count > 0 ? $"{binding} {request} request, " : string.Empty;
        var send = spec.Input.Count > 0 ? "request" : $"new {request}()";
        var body = new StringBuilder();
        if (hasResult)
        {
            body.AppendLine($"    [ProducesResponseType(typeof({resultType}), StatusCodes.Status200OK)]");
            body.AppendLine($"    public async Task<ActionResult<{resultType}>> {actionName}({parameter}CancellationToken cancellationToken) =>");
            body.Append($"        Ok(await sender.Send({send}, cancellationToken));");
        }
        else
        {
            body.AppendLine("    [ProducesResponseType(StatusCodes.Status204NoContent)]");
            body.AppendLine($"    public async Task<IActionResult> {actionName}({parameter}CancellationToken cancellationToken)");
            body.AppendLine("    {");
            body.AppendLine($"        await sender.Send({send}, cancellationToken);");
            body.AppendLine("        return NoContent();");
            body.Append("    }");
        }

        return body.ToString();
    }

    private static string OutputText(UseCaseOutput output) => output.Kind switch
    {
        UseCaseOutputKind.Generated => string.Join(",", output.Properties.Select(p => $"{p.Name}:{p.Type}")),
        UseCaseOutputKind.Existing => output.Type,
        UseCaseOutputKind.None => "none",
        _ => string.Empty,
    };

    /// <summary>Which namespaces the types of a use case need: the DTOs of the feature (a DTO name), the common enums (a name found under an <c>Enums</c> folder).</summary>
    private static (bool Dtos, bool Enums) ClassifyTypes(SolutionConfig config, UseCaseSpec spec)
    {
        var known = new HashSet<string>(StringComparer.Ordinal) { "string", "int", "long", "bool", "decimal", "double", "float", "Guid", "DateTime", "DateTimeOffset", "TimeSpan", "List", "Dictionary" };
        var enums = new HashSet<string>(StringComparer.Ordinal);
        var src = Path.Combine(config.SolutionPath, "src");
        if (Directory.Exists(src))
        {
            foreach (var file in Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Replace('\\', '/').Contains("/Enums/", StringComparison.Ordinal))
                    enums.Add(Path.GetFileNameWithoutExtension(file));
            }
        }

        var identifiers = spec.Input.Select(p => p.Type)
            .Concat(spec.Output.Properties.Select(p => p.Type))
            .Concat(spec.Output.Kind == UseCaseOutputKind.Existing ? new[] { spec.Output.Type } : Array.Empty<string>())
            .SelectMany(type => Regex.Matches(type, "[A-Za-z_][A-Za-z0-9_]*").Select(m => m.Value))
            .Where(name => !known.Contains(name))
            .ToList();
        return (spec.Output.Kind == UseCaseOutputKind.EntityDto || identifiers.Any(name => !enums.Contains(name)), identifiers.Any(enums.Contains));
    }

    private static string Summary(string? description, string actionName, string entity)
    {
        var text = string.IsNullOrWhiteSpace(description) ? $"{Regex.Replace(actionName, "(?<=[a-z0-9])(?=[A-Z])", " ")} ({entity.ToLowerInvariant()})." : description.Trim();
        return Regex.Replace(text, "[\"<>&\\r\\n\\\\]", " ").Trim();
    }
}
