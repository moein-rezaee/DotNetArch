using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace IdentityService.Mcp.Mcp;

public sealed class McpApprovalVerifier(
    IHttpContextAccessor httpContextAccessor,
    IConfiguration configuration,
    McpSignedEnvelopeVerifier envelopeVerifier,
    IMcpNonceStore nonceStore)
{
    private readonly AsyncLocal<McpApprovalVerification?> _lastVerification = new();

    public McpApprovalVerification LastVerification
        => _lastVerification.Value ?? new McpApprovalVerification(false, "required", null, "approval_required");

    public void Reset() => _lastVerification.Value = null;

    public McpApprovalVerification Verify(
        McpExecutionContext context,
        Guid targetUserId,
        Guid? sessionId,
        bool allOtherSessions,
        string? argumentApproval)
    {
        if (!allOtherSessions && !sessionId.HasValue)
        {
            return Set(new McpApprovalVerification(false, "denied", null, "invalid_revocation_scope"));
        }

        // A self-admin actor must never be able to approve revocation of its own
        // current session without the server having resolved that session first.
        if (allOtherSessions &&
            context.Subject == targetUserId &&
            Guid.TryParse(context.Actor, out var actorId) &&
            actorId == targetUserId &&
            !context.CurrentSessionId.HasValue)
        {
            return Set(new McpApprovalVerification(false, "denied", null, "approval_current_session_required"));
        }

        var expectedResource = allOtherSessions
            ? $"user:{targetUserId:D}/other-sessions"
            : $"user:{targetUserId:D}/session:{sessionId!.Value:D}";
        return Verify(context, "revoke_sessions", expectedResource, argumentApproval,
            allOtherSessions ? context.CurrentSessionId?.ToString("D") ?? string.Empty : null);
    }

    public McpApprovalVerification Verify(
        McpExecutionContext context,
        string expectedAction,
        string expectedResource,
        string? argumentApproval,
        string? expectedCurrentSessionId = null)
    {
        var compact = argumentApproval;
        if (string.IsNullOrWhiteSpace(compact))
        {
            compact = httpContextAccessor.HttpContext?.Request.Headers["X-Corevia-Mcp-Approval"].FirstOrDefault();
        }

        if (string.IsNullOrWhiteSpace(compact) && httpContextAccessor.HttpContext is null)
        {
            compact = Environment.GetEnvironmentVariable("COREVIA_MCP_APPROVAL")
                      ?? configuration["COREVIA_MCP_APPROVAL"];
        }

        if (!envelopeVerifier.TryRead(compact, "approval", out var payload, out var failureCode))
        {
            return Set(new McpApprovalVerification(false, "denied", null, failureCode));
        }

        var approvalId = McpSignedEnvelopeVerifier.GetString(payload, "approvalId");
        var actor = McpSignedEnvelopeVerifier.GetString(payload, "actor");
        var tool = McpSignedEnvelopeVerifier.GetString(payload, "tool");
        var version = McpSignedEnvelopeVerifier.GetString(payload, "version");
        var action = McpSignedEnvelopeVerifier.GetString(payload, "action");
        var resource = McpSignedEnvelopeVerifier.GetString(payload, "resource");
        var nonce = McpSignedEnvelopeVerifier.GetString(payload, "nonce");
        var boundCurrentSessionId = McpSignedEnvelopeVerifier.GetString(payload, "currentSessionId");
        var now = DateTimeOffset.UtcNow;

        if (string.IsNullOrWhiteSpace(approvalId) ||
            !string.Equals(actor, context.Actor, StringComparison.Ordinal) ||
            !string.Equals(McpSignedEnvelopeVerifier.GetString(payload, "audience"), context.Audience, StringComparison.Ordinal) ||
            !string.Equals(tool, context.ToolName, StringComparison.Ordinal) ||
            !string.Equals(version, context.ToolVersion, StringComparison.Ordinal) ||
            !string.Equals(action, expectedAction, StringComparison.Ordinal) ||
            !McpSignedEnvelopeVerifier.TryGetGuid(payload, "tenant", out var tenant) ||
            tenant != context.Tenant ||
            !McpSignedEnvelopeVerifier.TryGetTimestamp(payload, "issuedAt", out var issuedAt) ||
            !McpSignedEnvelopeVerifier.TryGetExpiry(payload, out var expiresAt) ||
            issuedAt > now.AddMinutes(1) ||
            issuedAt < now.AddMinutes(-5) ||
            expiresAt <= now ||
            expiresAt > now.AddMinutes(5) ||
            expiresAt <= issuedAt ||
            string.IsNullOrWhiteSpace(nonce) ||
            (expectedCurrentSessionId is not null && !string.Equals(boundCurrentSessionId, expectedCurrentSessionId, StringComparison.Ordinal)))
        {
            return Set(new McpApprovalVerification(false, "denied", approvalId,
                expectedCurrentSessionId is not null && !string.Equals(boundCurrentSessionId, expectedCurrentSessionId, StringComparison.Ordinal)
                    ? "approval_current_session_mismatch"
                    : "approval_binding_mismatch"));
        }

        if (!string.Equals(resource, expectedResource, StringComparison.Ordinal))
        {
            return Set(new McpApprovalVerification(false, "denied", approvalId, "approval_resource_mismatch"));
        }

        if (!nonceStore.TryConsume("approval", nonce, expiresAt))
        {
            return Set(new McpApprovalVerification(false, "denied", approvalId, "approval_replayed"));
        }

        return Set(new McpApprovalVerification(true, "approved", approvalId));
    }

    private McpApprovalVerification Set(McpApprovalVerification value)
    {
        _lastVerification.Value = value;
        return value;
    }
}
