namespace DotNetArch.Cli.Commands;

internal sealed class CiAddCommand : SolutionCommandBase
{
    public override bool Matches(string[] args) => CommandMatch.Is(args, "ci", "add");

    public override int Run(string[] args)
    {
        var parsed = CommandArgs.Parse(args, 2);
        var config = LoadConfig(parsed, out _);
        if (config == null)
            return 1;

        var provider = OpsGenerator.ResolveCiProvider(config, parsed.Get("ci") ?? parsed.Positionals.FirstOrDefault());
        if (provider is null)
        {
            ToolHost.Info("No CI provider selected.");
            return 0;
        }

        return OpsGenerator.AddCi(config, provider) ? 0 : 1;
    }
}
