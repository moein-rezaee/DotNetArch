using System.ComponentModel;
using {{App}}.Application.Common.Pagination;
using {{App}}.Application.Features.{{Plural}}.Commands.Create{{Entity}};
using {{App}}.Application.Features.{{Plural}}.Commands.Delete{{Entity}};
using {{App}}.Application.Features.{{Plural}}.Commands.Update{{Entity}};
using {{App}}.Application.Features.{{Plural}}.Dtos;
using {{App}}.Application.Features.{{Plural}}.Queries.Get{{Entity}}ById;
using {{App}}.Application.Features.{{Plural}}.Queries.Get{{Plural}};
using MediatR;
using ModelContextProtocol.Server;

namespace {{App}}.Mcp.Tools.{{Plural}};

/// <summary>MCP tools for {{Plural}}. They send the same MediatR requests as the API controllers, so behaviour is identical and logic is never duplicated.</summary>
[McpServerToolType]
public sealed partial class {{Plural}}Tools(ISender sender)
{
    [McpServerTool(Name = "{{ToolName}}_list", ReadOnly = true, Idempotent = true)]
    [Description("List {{PluralLower}}, one page at a time.")]
    public Task<PagedResult<{{Entity}}Dto>> List(
        [Description("1-based page number.")] int pageNumber = 1,
        [Description("Items per page, 1-100.")] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        ToolRunner.RunAsync(() => sender.Send(new Get{{Plural}}Query(pageNumber, pageSize), cancellationToken));

    [McpServerTool(Name = "{{ToolName}}_get", ReadOnly = true, Idempotent = true)]
    [Description("Get one {{EntityLower}} by id.")]
    public Task<{{Entity}}Dto> Get([Description("The {{EntityLower}} id.")] Guid id, CancellationToken cancellationToken = default) =>
        ToolRunner.RunAsync(() => sender.Send(new Get{{Entity}}ByIdQuery(id), cancellationToken));

    [McpServerTool(Name = "{{ToolName}}_create", ReadOnly = false, Destructive = false)]
    [Description("Create a {{EntityLower}}.")]
    public Task<{{Entity}}Dto> Create([Description("The {{EntityLower}} name.")] string name, CancellationToken cancellationToken = default) =>
        ToolRunner.RunAsync(() => sender.Send(new Create{{Entity}}Command(name), cancellationToken));

    [McpServerTool(Name = "{{ToolName}}_update", ReadOnly = false, Destructive = false, Idempotent = true)]
    [Description("Rename a {{EntityLower}}.")]
    public Task<{{Entity}}Dto> Update([Description("The {{EntityLower}} id.")] Guid id, [Description("The new name.")] string name, CancellationToken cancellationToken = default) =>
        ToolRunner.RunAsync(() => sender.Send(new Update{{Entity}}Command(id, name), cancellationToken));

    [McpServerTool(Name = "{{ToolName}}_delete", ReadOnly = false, Destructive = true, Idempotent = true)]
    [Description("Delete a {{EntityLower}}. This cannot be undone.")]
    public Task<string> Delete([Description("The {{EntityLower}} id.")] Guid id, CancellationToken cancellationToken = default) =>
        ToolRunner.RunAsync(() => sender.Send(new Delete{{Entity}}Command(id), cancellationToken), "Deleted.");
}
