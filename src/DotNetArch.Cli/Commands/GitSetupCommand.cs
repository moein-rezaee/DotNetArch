namespace DotNetArch.Cli.Commands;

internal sealed class GitSetupCommand : SolutionCommandBase
{
    public override bool Matches(string[] args) => CommandMatch.Is(args, "git", "setup");

    public override int Run(string[] args)
    {
        var parsed = CommandArgs.Parse(args, 2);
        var config = LoadConfig(parsed, out _);
        if (config == null)
            return 1;

        return OpsGenerator.SetupGit(config, parsed.Get("remote"), parsed.Get("git-host"), parsed.Get("git-provider")) ? 0 : 1;
    }
}
