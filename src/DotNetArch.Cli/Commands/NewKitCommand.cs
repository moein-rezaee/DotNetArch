using DotNetArch.Core.Scaffolding.Kits;

namespace DotNetArch.Cli.Commands;

internal sealed class NewKitCommand : ICommand
{
    public bool Matches(string[] args) => CommandMatch.Is(args, "new", "kit");

    public int Run(string[] args)
    {
        var parsed = CommandArgs.Parse(args, 2);

        var area = parsed.Get("area") ?? parsed.Positionals.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(area))
            area = ToolHost.Ask("Enter kit area (for example MediaStorage, Cache, MessageBroker)");
        if (string.IsNullOrWhiteSpace(area))
        {
            ToolHost.Error("Kit area is required.");
            return 1;
        }

        // Inside a solution the kit goes to <solution>/kits/<Area>; elsewhere to <output or cwd>/kits/<Area>.
        var outputPath = parsed.Get("output");
        if (string.IsNullOrWhiteSpace(outputPath))
            outputPath = PathState.Load() ?? Directory.GetCurrentDirectory();
        var config = ConfigManager.Load(outputPath!);
        var root = config?.SolutionPath ?? outputPath!;

        var prefix = parsed.Get("kit-prefix") ?? config?.KitPrefix;
        if (string.IsNullOrWhiteSpace(prefix))
            prefix = config?.SolutionName;
        if (string.IsNullOrWhiteSpace(prefix))
            prefix = ToolHost.Ask("Enter the package prefix (for example your company or product name)");
        if (string.IsNullOrWhiteSpace(prefix))
        {
            ToolHost.Error("A kit prefix is required (--kit-prefix).");
            return 1;
        }

        var providers = (parsed.Get("providers") ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var major = config is null ? 8 : PackageVersionResolver.ResolveTargetMajor(config.TargetFramework);

        var info = KitGenerator.Generate(new KitRequest(root, area, providers, prefix, major, parsed.Has("with-tests")));

        if (config is not null && !parsed.Has("no-wire"))
        {
            if (!KitWiring.Wire(config, info))
                return 1;
        }
        else
        {
            ToolHost.Info("Kit generated outside a solution; wire it with 'add kit' once it is inside one.");
        }

        return 0;
    }
}
