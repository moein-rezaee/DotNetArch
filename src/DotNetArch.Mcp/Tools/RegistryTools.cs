using System.Text.Json;
using System.Text.Json.Nodes;
using DotNetArch.Core.Operations;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Server;

namespace DotNetArch.Mcp.Tools;

/// <summary>
/// Builds one MCP tool per registry operation (D-20): the name, description, parameters and read-only/mutating nature come from the same
/// definition the CLI uses. Mutating tools take <c>apply</c> (default false = plan only) and always return the plan as JSON.
/// </summary>
public static class RegistryTools
{
    public static IEnumerable<McpServerTool> Create() =>
        OperationRegistry.All.Select(operation => McpServerTool.Create(
            new OperationFunction(operation),
            new McpServerToolCreateOptions
            {
                Name = operation.Name,
                ReadOnly = operation.Kind == OperationKind.ReadOnly,
                Destructive = false,
                Idempotent = true,
            }));

    private sealed class OperationFunction(OperationDefinition operation) : AIFunction
    {
        public override string Name => operation.Name;

        public override string Description => operation.Description;

        public override JsonElement JsonSchema { get; } = BuildSchema(operation);

        protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var parameter in operation.Parameters.Where(p => !p.CliOnly))
            {
                if (!arguments.TryGetValue(parameter.Name, out var raw) || raw is null)
                    continue;
                var text = raw is JsonElement element
                    ? (element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString())
                    : raw.ToString();
                if (text != null)
                    values[parameter.Name] = text;
            }

            var apply = arguments.TryGetValue("apply", out var applyRaw) && applyRaw is not null
                && (applyRaw is bool b ? b : applyRaw.ToString()!.Equals("true", StringComparison.OrdinalIgnoreCase));
            var result = OperationRegistry.Execute(operation, values, apply);
            return ValueTask.FromResult<object?>(OperationRegistry.ToJson(operation, result));
        }

        private static JsonElement BuildSchema(OperationDefinition definition)
        {
            var properties = new JsonObject();
            var required = new JsonArray();
            foreach (var parameter in definition.Parameters.Where(p => !p.CliOnly))
            {
                var property = new JsonObject
                {
                    ["type"] = parameter.Type == ParameterType.Flag ? "boolean" : "string",
                    ["description"] = parameter.Default is { } fallback && !fallback.StartsWith('@') ? $"{parameter.Description} Default: {fallback}." : parameter.Description,
                };
                if (parameter.Choices is { Count: > 0 } choices && parameter.Type == ParameterType.Text)
                    property["enum"] = new JsonArray(choices.Select(c => (JsonNode)JsonValue.Create(c)!).ToArray());
                properties[parameter.Name] = property;
                if (parameter.Required)
                    required.Add(parameter.Name);
            }

            if (definition.Kind == OperationKind.Mutating)
            {
                properties["apply"] = new JsonObject
                {
                    ["type"] = "boolean",
                    ["description"] = "false (default): return the plan only. true: write the planned changes.",
                };
            }

            var schema = new JsonObject { ["type"] = "object", ["properties"] = properties, ["required"] = required };
            return JsonSerializer.SerializeToElement(schema);
        }
    }
}
