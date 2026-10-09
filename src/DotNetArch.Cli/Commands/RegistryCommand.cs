using DotNetArch.Core.Operations;

namespace DotNetArch.Cli.Commands;

/// <summary>
/// Runs any operation of the registry (<c>doctor</c>, <c>adopt</c>, <c>fix</c>, ...): arguments are mapped onto the operation's declared parameters,
/// <c>--json</c> prints the machine-readable result, <c>--apply</c> writes (mutating operations only), <c>--out=file</c> saves the output.
/// Exit codes: 0 ok, 1 usage error, 3 blocking findings.
/// </summary>
internal sealed class RegistryCommand : ICommand
{
    public bool Matches(string[] args) => args.Length >= 1 && OperationRegistry.Find(args[0]) != null;

    public int Run(string[] args)
    {
        var operation = OperationRegistry.Find(args[0])!;
        var parsed = CommandArgs.Parse(args, 1);
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var positionals = new Queue<string>(parsed.Positionals);
        foreach (var parameter in operation.Parameters)
        {
            if (parameter.Type == ParameterType.Flag)
            {
                values[parameter.Name] = parsed.Has(parameter.Name) ? "true" : "false";
                continue;
            }

            var value = parsed.Get(parameter.Name) ?? (parameter.Positional && positionals.Count > 0 ? positionals.Dequeue() : null);
            if (value != null)
                values[parameter.Name] = value;
        }

        var result = OperationRegistry.Execute(operation, values, parsed.Has("apply"));
        if (result.Error != null && result.ExitCode == 1)
        {
            System.Console.Error.WriteLine(result.Error);
            return 1;
        }

        var output = parsed.Has("json") ? OperationRegistry.ToJson(operation, result) + Environment.NewLine : result.Text.TrimEnd() + Environment.NewLine;
        if (parsed.Get("out") is { Length: > 0 } outFile)
            File.WriteAllText(outFile, output);
        else
            System.Console.Out.Write(output);
        return result.ExitCode;
    }
}
