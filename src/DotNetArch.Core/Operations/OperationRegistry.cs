using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotNetArch.Core.Operations;

public static class OperationRegistry
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static IReadOnlyList<OperationDefinition> All { get; } = new[]
    {
        DoctorOperation.Definition,
        AdoptOperation.Definition,
        FixOperation.Definition,
    };

    public static OperationDefinition? Find(string name) =>
        All.FirstOrDefault(o => o.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Runs an operation. A mutating operation without <paramref name="apply"/> only returns its plan.</summary>
    public static OperationResult Execute(OperationDefinition operation, IReadOnlyDictionary<string, string> values, bool apply)
    {
        foreach (var parameter in operation.Parameters.Where(p => p.Required))
        {
            if (!values.TryGetValue(parameter.Name, out var value) || string.IsNullOrWhiteSpace(value))
                return new OperationResult(false, string.Empty, ExitCode: 1, Error: $"Missing required value '{parameter.Name}'.");
        }

        try
        {
            return operation.Run(new OperationRequest(values, apply && operation.Kind == OperationKind.Mutating));
        }
        catch (ArgumentException ex)
        {
            return new OperationResult(false, string.Empty, ExitCode: 1, Error: ex.Message);
        }
    }

    /// <summary>The machine-readable form returned by <c>--json</c> and by the MCP tools.</summary>
    public static string ToJson(OperationDefinition operation, OperationResult result) =>
        JsonSerializer.Serialize(
            new
            {
                result.Ok,
                Operation = operation.Name,
                result.Applied,
                result.Error,
                result.Plan,
                result.Data,
            },
            Json);
}
