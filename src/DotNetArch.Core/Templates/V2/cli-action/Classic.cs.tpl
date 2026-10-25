using {{App}}.Application.Features.{{Plural}}.Actions.{{ActionName}}{{Entity}};
using MediatR;

namespace {{App}}.Cli.Commands.{{Plural}};

internal sealed class {{ActionName}}{{Entity}}CliCommand : CliCommand
{
    public override string Group => "{{CliGroup}}";

    public override string Name => "{{ActionRoute}}";

    public override string Description => "{{ActionName}} a {{EntityLower}} (same use case as the API endpoint).";

    public override IReadOnlyList<CliOption> Options { get; } = [new("id", "guid", true, "The {{EntityLower}} id.")];

    public override Task<object?> RunAsync(CliContext context, ISender sender, CancellationToken cancellationToken) =>
        Box(sender.Send(new {{ActionName}}{{Entity}}{{RequestKind}}(context.Require<Guid>("id")), cancellationToken));
}
