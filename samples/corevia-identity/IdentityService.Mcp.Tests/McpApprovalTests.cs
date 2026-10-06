using IdentityService.Mcp.Mcp;
using Microsoft.Extensions.Options;

namespace IdentityService.Mcp.Tests;

public sealed class McpApprovalTests
{
    [Fact]
    public void Approval_is_bound_to_tool_action_resource_audience_and_nonce()
    {
        var actor = Guid.NewGuid().ToString("D");
        var target = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var session = Guid.NewGuid();
        var context = McpTestHelpers.Context(actor, target, tenant);
        var verifier = new McpApprovalVerifier(
            McpTestHelpers.EmptyHttpContextAccessor(),
            McpTestHelpers.EmptyConfiguration(),
            new McpSignedEnvelopeVerifier(Options.Create(McpTestHelpers.Options())),
            new InMemoryMcpNonceStore());
        var payload = McpTestHelpers.ApprovalPayload(
            actor,
            tenant,
            context.ToolName,
            $"user:{target:D}/session:{session:D}");
        var approval = McpTestHelpers.SignedEnvelope(payload, McpTestHelpers.SigningKey);

        var result = verifier.Verify(context, target, session, false, approval);

        Assert.True(result.IsValid);
        Assert.Equal("approved", result.Status);
        Assert.Equal("approval-123", result.ApprovalId);

        var replay = verifier.Verify(context, target, session, false, approval);
        Assert.False(replay.IsValid);
        Assert.Equal("approval_replayed", replay.FailureCode);
    }

    [Fact]
    public void Wrong_resource_or_missing_approval_fails_closed()
    {
        var actor = Guid.NewGuid().ToString("D");
        var target = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var session = Guid.NewGuid();
        var context = McpTestHelpers.Context(actor, target, tenant);
        var verifier = new McpApprovalVerifier(
            McpTestHelpers.EmptyHttpContextAccessor(),
            McpTestHelpers.EmptyConfiguration(),
            new McpSignedEnvelopeVerifier(Options.Create(McpTestHelpers.Options())),
            new InMemoryMcpNonceStore());

        var missing = verifier.Verify(context, target, session, false, null);
        Assert.False(missing.IsValid);
        Assert.Equal("missing_signed_context", missing.FailureCode);

        var payload = McpTestHelpers.ApprovalPayload(actor, tenant, context.ToolName, "user:wrong/session:wrong");
        var wrongResource = McpTestHelpers.SignedEnvelope(payload, McpTestHelpers.SigningKey);
        var mismatch = verifier.Verify(context, target, session, false, wrongResource);
        Assert.False(mismatch.IsValid);
        Assert.Equal("approval_resource_mismatch", mismatch.FailureCode);
    }

    [Fact]
    public void Other_sessions_approval_is_bound_to_the_authenticated_current_session()
    {
        var actor = Guid.NewGuid().ToString("D");
        var target = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var currentSession = Guid.NewGuid();
        var context = McpTestHelpers.Context(actor, target, tenant) with
        {
            CurrentSessionId = currentSession
        };
        var verifier = new McpApprovalVerifier(
            McpTestHelpers.EmptyHttpContextAccessor(),
            McpTestHelpers.EmptyConfiguration(),
            new McpSignedEnvelopeVerifier(Options.Create(McpTestHelpers.Options())),
            new InMemoryMcpNonceStore());

        var payload = McpTestHelpers.ApprovalPayload(
            actor,
            tenant,
            context.ToolName,
            $"user:{target:D}/other-sessions",
            currentSessionId: currentSession);
        var approval = McpTestHelpers.SignedEnvelope(payload, McpTestHelpers.SigningKey);

        var valid = verifier.Verify(context, target, null, true, approval);

        Assert.True(valid.IsValid);
        Assert.Equal("approved", valid.Status);

        var wrongSessionPayload = McpTestHelpers.ApprovalPayload(
            actor,
            tenant,
            context.ToolName,
            $"user:{target:D}/other-sessions",
            nonce: "approval-2",
            currentSessionId: Guid.NewGuid());
        var wrongSession = verifier.Verify(
            context,
            target,
            null,
            true,
            McpTestHelpers.SignedEnvelope(wrongSessionPayload, McpTestHelpers.SigningKey));

        Assert.False(wrongSession.IsValid);
        Assert.Equal("approval_current_session_mismatch", wrongSession.FailureCode);
    }

    [Fact]
    public void Other_sessions_for_the_actor_require_a_server_resolved_current_session()
    {
        var target = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        var context = McpTestHelpers.Context(target.ToString("D"), target, tenant);
        var verifier = new McpApprovalVerifier(
            McpTestHelpers.EmptyHttpContextAccessor(),
            McpTestHelpers.EmptyConfiguration(),
            new McpSignedEnvelopeVerifier(Options.Create(McpTestHelpers.Options())),
            new InMemoryMcpNonceStore());

        var result = verifier.Verify(context, target, null, true, "ignored");

        Assert.False(result.IsValid);
        Assert.Equal("approval_current_session_required", result.FailureCode);
    }
}
