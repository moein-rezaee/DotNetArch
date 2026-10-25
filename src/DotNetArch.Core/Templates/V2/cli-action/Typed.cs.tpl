using {{App}}.Application.Features.{{Plural}}.Actions.{{ActionName}}{{Entity}};
{{DtoUsing}}using MediatR;

namespace {{App}}.Cli.Commands.{{Plural}};

internal sealed class {{ActionName}}{{Entity}}CliCommand : CliCommand
{
    public override string Group => "{{CliGroup}}";

    public override string Name => "{{ActionRoute}}";

    public override string Description => "{{Summary}}";

    public override IReadOnlyList<CliOption> Options { get; } =
    [
{{CliOptions}}
    ];

    public override {{CliRunSignature}}
    {
{{CliReads}}
        {{CliSend}}
    }
}
