namespace DotNetArch.Cli.Commands;

internal sealed class ExecCommand : SolutionCommandBase
{
    public override bool Matches(string[] args) => CommandMatch.Is(args, "exec");

    public override int Run(string[] args)
    {
        var parsed = CommandArgs.Parse(args, 1);
        var dockerStop = parsed.Has("docker-stop");
        var detach = parsed.Has("docker-detach");
        var useDocker = parsed.Has("docker") || detach || dockerStop;

        var config = LoadConfig(parsed, out _);
        if (config == null)
            return 1;

        // Ctrl+C stops the running child process; Core then tears docker resources down in its finally block.
        ConsoleCancelEventHandler? handler = null;
        if (useDocker && !detach && !dockerStop)
        {
            handler = (_, e) =>
            {
                e.Cancel = true;
                ToolHost.Runner.Cancel();
            };
            System.Console.CancelKeyPress += handler;
        }

        try
        {
            ExecService.Run(config, new ExecOptions(useDocker, detach, dockerStop));
            return 0;
        }
        finally
        {
            if (handler != null)
                System.Console.CancelKeyPress -= handler;
        }
    }
}
