using DotNetArch.Cli.Commands;
using DotNetArch.Cli.Console;

namespace DotNetArch.Cli;

internal static class Program
{
    private static readonly ICommand[] Commands =
    {
        new NewCrudCommand(),
        new NewEventCommand(),
        new NewEnumCommand(),
        new NewConstantCommand(),
        new NewActionCommand(),
        new NewServiceCommand(),
        new ExecCommand(),
        new RemoveMigrationCommand(),
        new NewKitCommand(),
        new AddKitCommand(),
        new AddMcpCommand(),
        new CiAddCommand(),
        new DockerAddCommand(),
        new GitSetupCommand(),
        new NewSolutionCommand(),
        new InteractiveSolutionCommand(), // fallback, must stay last
    };

    private static int Main(string[] args)
    {
        // The MCP server owns stdout (protocol frames): handle it before any console output can happen.
        if (args.Length >= 2 && args[0].Equals("mcp", StringComparison.OrdinalIgnoreCase) && args[1].Equals("serve", StringComparison.OrdinalIgnoreCase))
            return DotNetArch.Mcp.McpServerHost.RunAsync(args[2..]).GetAwaiter().GetResult();

        var output = new ConsoleToolOutput();
        ToolHost.Configure(new HostContext(new ConsolePrompter(output), output, new ConsoleProcessRunner()));

        if (!SolutionTooling.EnsureDotnetSdk())
            return 2;

        try
        {
            return Commands.First(command => command.Matches(args)).Run(args);
        }
        catch (MissingInputException ex)
        {
            ToolHost.Error(ex.Message);
            return 1;
        }
        catch (ArgumentException ex)
        {
            ToolHost.Error(ex.Message);
            return 1;
        }
    }
}
