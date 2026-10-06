using DotNetArch.Core.Scaffolding.Kits;

namespace DotNetArch.Cli.Commands;

internal sealed class AddKitCommand : SolutionCommandBase
{
    public override bool Matches(string[] args) => CommandMatch.Is(args, "add", "kit");

    public override int Run(string[] args)
    {
        var parsed = CommandArgs.Parse(args, 2);
        var config = LoadConfig(parsed, out _);
        if (config == null)
            return 1;

        var area = parsed.Get("area") ?? parsed.Positionals.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(area))
            area = ToolHost.Ask("Enter the kit area to wire");
        if (string.IsNullOrWhiteSpace(area))
        {
            ToolHost.Error("Kit area is required.");
            return 1;
        }

        return KitWiring.WireFromDisk(config, KitGenerator.NormalizeArea(area)) ? 0 : 1;
    }
}
