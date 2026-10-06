using DotNetArch.Core.Scaffolding.V2;

namespace DotNetArch.Cli.Commands;

internal sealed class NewServiceCommand : SolutionCommandBase
{
    public override bool Matches(string[] args) => CommandMatch.Is(args, "new", "service");

    public override int Run(string[] args)
    {
        var parsed = CommandArgs.Parse(args, 2);
        var config = LoadConfig(parsed, out _);
        if (config == null)
            return 1;

        if (!config.IsV2)
        {
            ServiceScaffolder.Generate(config);
            return 0;
        }

        // Explicit options skip the questions (scripts, agents): --logic=true|false decides internal service vs kit.
        if (bool.TryParse(parsed.Get("logic"), out var hasLogic))
        {
            if (hasLogic)
            {
                var name = parsed.Get("name");
                if (string.IsNullOrWhiteSpace(name))
                {
                    ToolHost.Error("--name is required for a service with business logic.");
                    return 1;
                }

                return ServiceV2Generator.GenerateInternal(config, parsed.Get("entity"), name, parsed.Get("lifetime") ?? "Scoped") ? 0 : 1;
            }

            var area = parsed.Get("area");
            if (string.IsNullOrWhiteSpace(area))
            {
                ToolHost.Error("--area is required for an external service (kit).");
                return 1;
            }

            var providers = (parsed.Get("providers") ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return ServiceV2Generator.GenerateKit(config, area, providers, parsed.Has("with-tests")) ? 0 : 1;
        }

        return ServiceV2Generator.Run(config) ? 0 : 1;
    }
}
