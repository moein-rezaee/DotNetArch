using System.Text.Json;
using {{App}}.Application.Common.Exceptions;
using {{App}}.Domain.Common;
using FluentValidation;
using MediatR;

namespace {{App}}.Cli;

/// <summary>
/// Runs one command line: finds the command, parses its options, sends the request and prints the result as JSON.
/// Exit codes: 0 done, 1 the use case refused (validation, not found, domain rule), 2 usage error.
/// </summary>
public sealed class CliApp(IEnumerable<CliCommand> commands, ISender sender, TextWriter output, TextWriter error)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly List<CliCommand> _commands = commands.OrderBy(c => c.Group, StringComparer.Ordinal).ThenBy(c => c.Name, StringComparer.Ordinal).ToList();

    public IReadOnlyList<CliCommand> Commands => _commands;

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        var positional = args.TakeWhile(a => !a.StartsWith("--", StringComparison.Ordinal)).ToList();
        if (positional.Count == 0 || positional[0] is "help" or "-h")
        {
            WriteHelp(positional.Count > 1 ? positional[1] : null);
            return positional.Count == 0 && args.Length == 0 ? 2 : 0;
        }

        if (args.Contains("--help", StringComparer.Ordinal))
        {
            WriteHelp(positional[0]);
            return 0;
        }

        var command = positional.Count >= 2 ? _commands.FirstOrDefault(c => c.Group == positional[0] && c.Name == positional[1]) : null;
        if (command is null)
        {
            await error.WriteLineAsync($"Unknown command '{string.Join(' ', positional)}'. Run '{{CliName}} help'.");
            return 2;
        }

        try
        {
            var context = new CliContext(ParseOptions(args.Skip(positional.Count)));
            var result = await command.RunAsync(context, sender, cancellationToken);
            await output.WriteLineAsync(result is string text ? text : JsonSerializer.Serialize(result, JsonOptions));
            return 0;
        }
        catch (CliUsageException exception)
        {
            await error.WriteLineAsync(exception.Message);
            return 2;
        }
        catch (ValidationException exception)
        {
            await error.WriteLineAsync("Validation failed: " + string.Join("; ", exception.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}")));
            return 1;
        }
        catch (Exception exception) when (exception is NotFoundException or DomainException)
        {
            await error.WriteLineAsync(exception.Message);
            return 1;
        }
    }

    private static Dictionary<string, string> ParseOptions(IEnumerable<string> tokens)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        string? pending = null;
        foreach (var token in tokens)
        {
            if (token.StartsWith("--", StringComparison.Ordinal))
            {
                if (pending is not null)
                    values[pending] = string.Empty;

                var body = token[2..];
                var split = body.IndexOf('=');
                if (split >= 0)
                {
                    values[body[..split]] = body[(split + 1)..];
                    pending = null;
                }
                else
                {
                    pending = body;
                }
            }
            else if (pending is not null)
            {
                values[pending] = token;
                pending = null;
            }
            else
            {
                throw new CliUsageException($"Unexpected argument '{token}'. Options look like --name=value.");
            }
        }

        if (pending is not null)
            values[pending] = string.Empty;
        return values;
    }

    private void WriteHelp(string? group)
    {
        output.WriteLine("{{CliName}} <group> <command> [--option=value]");
        foreach (var commandGroup in _commands.Where(c => group is null || c.Group == group).GroupBy(c => c.Group))
        {
            output.WriteLine();
            output.WriteLine(commandGroup.Key);
            foreach (var command in commandGroup)
            {
                var options = string.Join(' ', command.Options.Select(o => o.Required ? $"--{o.Name}=<{o.Type}>" : $"[--{o.Name}=<{o.Type}>]"));
                output.WriteLine($"  {command.Name,-18} {command.Description} {options}".TrimEnd());
            }
        }
    }
}
