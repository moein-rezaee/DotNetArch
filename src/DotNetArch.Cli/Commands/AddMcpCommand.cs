using DotNetArch.Core.Scaffolding.V2;

namespace DotNetArch.Cli.Commands;

internal sealed class AddMcpCommand : SolutionCommandBase
{
    public override bool Matches(string[] args) => CommandMatch.Is(args, "add", "mcp");

    public override int Run(string[] args)
    {
        var config = LoadConfig(CommandArgs.Parse(args, 2), out _);
        if (config == null)
            return 1;

        return McpV2Generator.Add(config) ? 0 : 1;
    }
}
