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
/// <param name="CliOnly">Not offered to MCP agents (for example creating an empty layer: an agent migrates what belongs or does nothing).</param>
/// <param name="Alias">Another CLI spelling of the same value (for example <c>output</c> for <c>path</c>).</param>
/// <param name="Choices">The values the parameter accepts (shown in the MCP schema and offered by the interactive CLI).</param>
/// <param name="Default">Value used when none is given; <c>@cwd</c> means the current folder and is resolved by the CLI only.</param>
public sealed record OperationParameter(
    string Name,
    string Description,
    ParameterType Type = ParameterType.Text,
    bool Required = false,
    bool Positional = false,
    bool CliOnly = false,
    string? Alias = null,
    IReadOnlyList<string>? Choices = null,
    string? Default = null);

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
