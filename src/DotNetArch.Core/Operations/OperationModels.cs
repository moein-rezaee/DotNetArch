namespace DotNetArch.Core.Operations;

public enum OperationKind
{
    /// <summary>Never changes anything.</summary>
    ReadOnly,

    /// <summary>Changes files; always produces a plan first and writes only with apply.</summary>
    Mutating,
}

public enum ParameterType
{
    Text,
    Flag,
}

/// <param name="Positional">The CLI accepts the value as the first bare argument.</param>
public sealed record OperationParameter(string Name, string Description, ParameterType Type = ParameterType.Text, bool Required = false, bool Positional = false);

public sealed record OperationRequest(IReadOnlyDictionary<string, string> Values, bool Apply)
{
    public string? Get(string name) => Values.TryGetValue(name, out var value) && value.Length > 0 ? value : null;

    public bool Flag(string name) => Values.TryGetValue(name, out var value) && !value.Equals("false", StringComparison.OrdinalIgnoreCase);
}

/// <summary>One file the operation would create or modify (plan) or did (applied).</summary>
public sealed record PlannedChange(string Path, string Action, string Reason, string? RuleId = null);

public sealed record OperationResult(
    bool Ok,
    string Text,
    object? Data = null,
    IReadOnlyList<PlannedChange>? Plan = null,
    bool Applied = false,
    int ExitCode = 0,
    string? Error = null);

/// <summary>
/// The single definition of an operation (D-20). The CLI parses arguments into an <see cref="OperationRequest"/> and the MCP server derives a tool
/// from the same definition, so the two can never drift apart.
/// </summary>
public sealed record OperationDefinition(
    string Name,
    string Description,
    OperationKind Kind,
    IReadOnlyList<OperationParameter> Parameters,
    Func<OperationRequest, OperationResult> Run);
