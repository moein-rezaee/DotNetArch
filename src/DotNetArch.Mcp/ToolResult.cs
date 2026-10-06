namespace DotNetArch.Mcp;

/// <summary>What every tool returns: success flag, the equivalent CLI command, the progress log and the files that changed.</summary>
public sealed record ToolResult(
    bool Ok,
    string Command,
    string Output,
    IReadOnlyList<string> Created,
    IReadOnlyList<string> Modified,
    string? Error = null);
