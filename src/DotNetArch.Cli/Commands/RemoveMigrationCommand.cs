namespace DotNetArch.Cli.Commands;

internal sealed class RemoveMigrationCommand : SolutionCommandBase
{
    public override bool Matches(string[] args) => CommandMatch.Is(args, "remove", "migration");

    public override int Run(string[] args)
    {
        var config = LoadConfig(CommandArgs.Parse(args, 2), out var basePath);
        if (config == null)
            return 1;

        return MigrationService.RemoveLast(config, basePath) ? 0 : 1;
    }
}
