using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace IdentityService.Mcp.Mcp;

public sealed record McpAuditRecord(
    string? Actor,
    Guid? Subject,
    Guid? Tenant,
    string? Mode,
    string ToolName,
    string ToolVersion,
    IReadOnlyCollection<string> RequiredScopes,
    IReadOnlyCollection<string> EffectiveScopes,
    string AuthorizationDecision,
    string? ApprovalId,
    string ApprovalStatus,
    string ResultStatus,
    string CorrelationId,
    DateTimeOffset TimestampUtc,
    string? FailureCode = null);

public interface IMcpAuditSink
{
    Task WriteAsync(McpAuditRecord record, CancellationToken cancellationToken = default);
}

public sealed class StructuredMcpAuditSink(ILogger<StructuredMcpAuditSink> logger) : IMcpAuditSink
{
    public Task WriteAsync(McpAuditRecord record, CancellationToken cancellationToken = default)
    {
        // Only the allow-listed audit fields are logged. Raw MCP arguments, JWTs,
        // approval envelopes, and tool results are intentionally not included.
        logger.LogInformation(
            "MCP audit {AuditRecord}",
            JsonSerializer.Serialize(record));
        return Task.CompletedTask;
    }
}

public sealed class InMemoryMcpAuditSink : IMcpAuditSink
{
    private readonly List<McpAuditRecord> _records = [];
    private readonly object _gate = new();

    public IReadOnlyCollection<McpAuditRecord> Records
    {
        get
        {
            lock (_gate)
            {
                return _records.ToArray();
            }
        }
    }

    public Task WriteAsync(McpAuditRecord record, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _records.Add(record);
        }

        return Task.CompletedTask;
    }
}
