using System.ComponentModel;
using {{App}}.Application.Features.{{Plural}}.Actions.{{ActionName}}{{Entity}};
using {{App}}.Application.Features.{{Plural}}.Dtos;
using MediatR;
using ModelContextProtocol.Server;

namespace {{App}}.Mcp.Tools.{{Plural}};

public sealed partial class {{Plural}}Tools
{
    [McpServerTool(Name = "{{ToolName}}_{{ActionSnake}}", ReadOnly = {{IsReadOnly}}, Destructive = false)]
    [Description("{{ActionName}} a {{EntityLower}} (same use case as the API endpoint).")]
    public Task<{{Entity}}Dto> {{ActionName}}([Description("The {{EntityLower}} id.")] Guid id, CancellationToken cancellationToken = default) =>
        ToolRunner.RunAsync(() => sender.Send(new {{ActionName}}{{Entity}}{{RequestKind}}(id), cancellationToken));
}
