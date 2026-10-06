using Corevia.Kit.ErrorHandling.Abstractions.Exceptions;
using IdentityService.Mcp.Mcp;

namespace IdentityService.Mcp.Tests;

public sealed class McpTransportSelectorTests
{
    [Theory]
    [InlineData(null, "Production")]
    [InlineData("", "Production")]
    [InlineData("  ", "Development")]
    [InlineData("http", "Production")]
    [InlineData("HTTP", "Staging")]
    public void Http_is_the_default_and_allowed_everywhere(string? configured, string environment)
    {
        Assert.Equal(McpTransportKind.Http, McpTransportSelector.Resolve(configured, environment));
    }

    [Theory]
    [InlineData("stdio", "Development")]
    [InlineData("STDIO", "development")]
    public void Stdio_is_allowed_only_in_development(string configured, string environment)
    {
        Assert.Equal(McpTransportKind.Stdio, McpTransportSelector.Resolve(configured, environment));
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    [InlineData(null)]
    public void Stdio_outside_development_is_rejected_with_bad_request(string? environment)
    {
        var exception = Assert.Throws<BadRequestException>(() => McpTransportSelector.Resolve("stdio", environment));
        Assert.Equal("mcp_stdio_dev_only", exception.ErrorCode);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, exception.StatusCode);
    }

    [Fact]
    public void Unknown_transport_is_rejected_with_bad_request()
    {
        var exception = Assert.Throws<BadRequestException>(() => McpTransportSelector.Resolve("websocket", "Development"));
        Assert.Equal("mcp_transport_unsupported", exception.ErrorCode);
    }
}
