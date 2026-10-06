using System.Text.Json.Nodes;
using IdentityService.Mcp.Mcp;
using Microsoft.Extensions.Options;

namespace IdentityService.Mcp.Tests;

public sealed class McpSignedEnvelopeTests
{
    [Fact]
    public void Reads_valid_envelope_and_rejects_wrong_signature_or_purpose()
    {
        var verifier = new McpSignedEnvelopeVerifier(Options.Create(McpTestHelpers.Options()));
        var payload = new JsonObject
        {
            ["type"] = "delegation",
            ["nonce"] = "n1"
        };
        var compact = McpTestHelpers.SignedEnvelope(payload, McpTestHelpers.SigningKey);

        Assert.True(verifier.TryRead(compact, "delegation", out var read, out var failure));
        Assert.Equal("n1", McpSignedEnvelopeVerifier.GetString(read, "nonce"));
        Assert.Empty(failure);

        Assert.False(verifier.TryRead(compact[..^1] + "A", "delegation", out _, out var badSignature));
        Assert.Equal("invalid_signed_context_signature", badSignature);
        Assert.False(verifier.TryRead(compact, "approval", out _, out var badPurpose));
        Assert.Equal("invalid_signed_context_type", badPurpose);
    }

    [Fact]
    public void Nonce_store_is_one_shot_per_purpose()
    {
        var store = new InMemoryMcpNonceStore();
        var expiry = DateTimeOffset.UtcNow.AddMinutes(1);

        Assert.True(store.TryConsume("delegation", "nonce-1", expiry));
        Assert.False(store.TryConsume("delegation", "nonce-1", expiry));
        Assert.True(store.TryConsume("approval", "nonce-1", expiry));
    }
}
