using MediatR;
using ModelContextProtocol.Server;

namespace {{App}}.Mcp.Tools.{{Plural}};

/// <summary>MCP tools of the {{Entity}} use cases. They send the same MediatR requests as the API controller, so behaviour is identical.</summary>
[McpServerToolType]
public sealed partial class {{Plural}}Tools(ISender sender)
{
}
