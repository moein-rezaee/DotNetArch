using System.ComponentModel;
using {{App}}.Application.Features.{{Plural}}.Actions.{{ActionName}}{{Entity}};
{{DtoUsing}}using MediatR;
using ModelContextProtocol.Server;

namespace {{App}}.Mcp.Tools.{{Plural}};

public sealed partial class {{Plural}}Tools
{
    [McpServerTool(Name = "{{ToolName}}_{{ActionSnake}}", ReadOnly = {{IsReadOnly}}, Destructive = false)]
    [Description("{{Summary}}")]
    public {{McpReturn}} {{ActionName}}({{McpParameters}}) =>
        ToolRunner.RunAsync(() => sender.Send(new {{ActionName}}{{Entity}}{{RequestKind}}({{NamedArguments}}), cancellationToken){{McpSuccess}});
}
