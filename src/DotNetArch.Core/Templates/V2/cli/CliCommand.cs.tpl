using MediatR;

namespace {{App}}.Cli;

/// <summary>An option of a command: <c>--name=value</c>.</summary>
public sealed record CliOption(string Name, string Type, bool Required, string Description);

/// <summary>
/// One command of the CLI: <c>{{CliName}} &lt;group&gt; &lt;command&gt; [--option=value]</c>. It sends the same MediatR request as the API controller and the
/// MCP tool of the same name (<see cref="Tool"/>), so behaviour and rules are identical and nothing is written twice.
/// </summary>
public abstract class CliCommand
{
    public abstract string Group { get; }

    public abstract string Name { get; }

    public abstract string Description { get; }

    public virtual IReadOnlyList<CliOption> Options => Array.Empty<CliOption>();

    /// <summary>The name of the MCP tool that does the same: <c>group_command</c> with underscores.</summary>
    public string Tool => $"{Group}_{Name}".Replace('-', '_');

    public abstract Task<object?> RunAsync(CliContext context, ISender sender, CancellationToken cancellationToken);

    /// <summary>Waits for a request and returns its result as an object (a command that returns nothing yields null).</summary>
    protected static async Task<object?> Box<T>(Task<T> request) => await request.ConfigureAwait(false);
}
