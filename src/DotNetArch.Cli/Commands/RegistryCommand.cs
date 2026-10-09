using DotNetArch.Core.Config;
using DotNetArch.Core.Operations;

namespace DotNetArch.Cli.Commands;

/// <summary>
/// Runs any operation of the registry: arguments are mapped onto the operation's declared parameters (a two-word command such as <c>new crud</c> is the
/// operation <c>new_crud</c>), missing required values are asked for, <c>--json</c> prints the machine-readable result, <c>--apply</c> writes (mutating
/// operations only; at a terminal the plan is shown and the user is asked), <c>--out=file</c> saves the output. Exit codes: 0 ok, 1 usage error, 2 .NET SDK
/// missing, 3 blocking findings.
/// </summary>
internal sealed class RegistryCommand : ICommand
{
    public bool Matches(string[] args) => Resolve(args, out _) != null;

    public int Run(string[] args)
    {
        var operation = Resolve(args, out var used)!;
        var parsed = CommandArgs.Parse(args, used);
        var values = Collect(operation, parsed);
        var json = parsed.Has("json");
        var apply = parsed.Has("apply");

        var result = OperationRegistry.Execute(operation, values, apply);
        if (result.Error != null && result.ExitCode == 1 && result.Text.Length == 0)
        {
            System.Console.Error.WriteLine(result.Error);
            return 1;
        }

        if (!json && !apply && operation.Kind == OperationKind.Mutating && result.Ok && result.Plan is { Count: > 0 } && !System.Console.IsInputRedirected && parsed.Get("out") == null)
        {
            System.Console.Out.WriteLine(result.Text.TrimEnd());
            if (DotNetArch.Core.Hosting.ToolHost.AskYesNo("Apply these changes?", false))
                result = OperationRegistry.Execute(operation, values, apply: true);
            else
                return result.ExitCode;
        }

        var output = json ? OperationRegistry.ToJson(operation, result) + Environment.NewLine : result.Text.TrimEnd() + Environment.NewLine;
        if (result.Error != null && !json)
            output += result.Error + Environment.NewLine;
        if (parsed.Get("out") is { Length: > 0 } outFile)
            File.WriteAllText(outFile, output);
        else
            System.Console.Out.Write(output);
        return result.ExitCode;
    }

    /// <summary>The operation named by the first one or two bare words (<c>new crud</c> is <c>new_crud</c>).</summary>
    private static OperationDefinition? Resolve(string[] args, out int used)
    {
        used = 1;
        if (args.Length >= 2 && !args[1].StartsWith("--", StringComparison.Ordinal) && OperationRegistry.Find($"{args[0]}_{args[1]}") is { } two)
        {
            used = 2;
            return two;
        }

        return args.Length >= 1 ? OperationRegistry.Find(args[0]) : null;
    }

    private static Dictionary<string, string> Collect(OperationDefinition operation, CommandArgs parsed)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var positionals = new Queue<string>(parsed.Positionals);
        foreach (var parameter in operation.Parameters)
        {
            if (parameter.Type == ParameterType.Flag)
            {
                values[parameter.Name] = parsed.Has(parameter.Name.Replace('_', '-')) || parsed.Has(parameter.Name) ? "true" : "false";
                continue;
            }

            var value = Lookup(parsed, parameter.Name) ?? (parameter.Alias != null ? Lookup(parsed, parameter.Alias) : null)
                ?? (parameter.Positional && positionals.Count > 0 ? positionals.Dequeue() : null);
            if (value == null && parameter.Default != null)
                value = parameter.Default switch
                {
                    "@cwd" => Directory.GetCurrentDirectory(),
                    "@saved" => PathState.Load() ?? Directory.GetCurrentDirectory(),
                    var fixedValue => fixedValue,
                };
            if (value == null && parameter.Required)
                value = Ask(parameter);
            if (!string.IsNullOrWhiteSpace(value))
                values[parameter.Name] = value;
        }

        return values;
    }

    private static string? Lookup(CommandArgs parsed, string name) => parsed.Get(name.Replace('_', '-')) ?? parsed.Get(name);

    private static string? Ask(OperationParameter parameter)
    {
        var text = parameter.Description.TrimEnd('.');
        var answer = parameter.Choices is { Count: > 0 } choices
            ? DotNetArch.Core.Hosting.ToolHost.AskOption($"{text}", choices.ToArray())
            : DotNetArch.Core.Hosting.ToolHost.Ask($"Enter {parameter.Name.Replace('_', ' ')} ({text})");
        return string.IsNullOrWhiteSpace(answer) ? null : answer;
    }
}
