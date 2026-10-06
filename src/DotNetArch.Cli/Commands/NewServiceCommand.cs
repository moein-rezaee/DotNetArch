namespace DotNetArch.Cli.Commands;

internal sealed class NewServiceCommand : SolutionCommandBase
{
    public override bool Matches(string[] args) => CommandMatch.Is(args, "new", "service");

    public override int Run(string[] args)
    {
        var config = LoadConfig(CommandArgs.Parse(args, 2), out _);
        if (config == null)
            return 1;

        ServiceScaffolder.Generate(config);
        return 0;
    }
}
