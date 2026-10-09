using DotNetArch.Cli.Commands;
using DotNetArch.Cli.Console;
using DotNetArch.Core.Operations;

namespace DotNetArch.Cli;

internal static class Program
{
    private static int Main(string[] args)
    {
        // The MCP server owns stdout (protocol frames): handle it before any console output can happen.
        if (args.Length >= 2 && args[0].Equals("mcp", StringComparison.OrdinalIgnoreCase) && args[1].Equals("serve", StringComparison.OrdinalIgnoreCase))
            return DotNetArch.Mcp.McpServerHost.RunAsync(args[2..]).GetAwaiter().GetResult();

        if (args.Length == 0 || args[0] is "help" or "--help" or "-h" or "commands")
            return Help(args.Length == 0 && !System.Console.IsInputRedirected);

        var output = new ConsoleToolOutput();
        ToolHost.Configure(new HostContext(new ConsolePrompter(output), output, new ConsoleProcessRunner()));

        var command = new RegistryCommand();
        if (!command.Matches(args))
        {
            System.Console.Error.WriteLine($"Unknown command '{args[0]}'. Run 'dotnet-arch help' for the list.");
            return 1;
        }

        try
        {
            return command.Run(args);
        }
        catch (MissingInputException ex)
        {
            System.Console.Error.WriteLine($"Missing value: {ex.Message}");
            return 1;
        }
        catch (ArgumentException ex)
        {
            System.Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    /// <summary>Lists every command of the registry (the same list the MCP server offers as tools).</summary>
    private static int Help(bool interactive)
    {
        System.Console.WriteLine("dotnet-arch <command> [values] [--apply] [--json] [--out=file]   (mutating commands plan first and write only with --apply)");
        System.Console.WriteLine();
        foreach (var operation in OperationRegistry.All)
        {
            var words = operation.Name.Replace('_', ' ');
            var kind = operation.Kind == OperationKind.ReadOnly ? "read-only" : "plans, --apply writes";
            var required = string.Join(' ', operation.Parameters.Where(p => p.Required && !p.Positional).Select(p => $"--{p.Name.Replace('_', '-')}=<..>"));
            var positional = string.Join(' ', operation.Parameters.Where(p => p.Positional).Select(p => $"<{p.Name}>"));
            System.Console.WriteLine($"  {words,-18} {kind,-22} {positional} {required}".TrimEnd());
        }

        System.Console.WriteLine("  mcp serve          MCP server over stdio (every command above is a tool: new_crud, doctor, add_layer, ...)");
        System.Console.WriteLine();
        System.Console.WriteLine("Details of one command: dotnet-arch help <command>. Interactive start (new solution): run without arguments at a terminal.");
        if (interactive)
        {
            var output = new ConsoleToolOutput();
            ToolHost.Configure(new HostContext(new ConsolePrompter(output), output, new ConsoleProcessRunner()));
            ToolHost.Info("Welcome to DotNetArch!");
            return new RegistryCommand().Run(new[] { "new", "solution" });
        }

        return 0;
    }
}
