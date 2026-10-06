using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace {{App}}.Mcp.Tests;

public class McpEndpointTests(McpFactory factory) : IClassFixture<McpFactory>
{
    private static StringContent Rpc(object request) => new(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

    private HttpClient Client(bool authorized)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        if (authorized)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", McpFactory.Token);
        return client;
    }

    /// <summary>The streamable HTTP transport answers either as plain JSON or as one server-sent event.</summary>
    private static JsonElement ParseMessage(string body)
    {
        var json = body.TrimStart().StartsWith('{')
            ? body
            : string.Join(string.Empty, body.Split('\n').Where(line => line.StartsWith("data:")).Select(line => line["data:".Length..].Trim()));
        return JsonDocument.Parse(json).RootElement.Clone();
    }

    [Fact]
    public async Task Health_is_open() =>
        Assert.Equal(HttpStatusCode.OK, (await Client(authorized: false).GetAsync("/health")).StatusCode);

    [Fact]
    public async Task The_mcp_endpoint_requires_the_bearer_token()
    {
        var response = await Client(authorized: false).PostAsync("/mcp", Rpc(new { jsonrpc = "2.0", id = 1, method = "tools/list" }));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_wrong_token_is_rejected()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "wrong");

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/mcp", Rpc(new { jsonrpc = "2.0", id = 1, method = "tools/list" }))).StatusCode);
    }

    [Fact]
    public async Task An_authorised_client_can_initialize_and_list_tools()
    {
        var client = Client(authorized: true);

        var init = await client.PostAsync("/mcp", Rpc(new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "initialize",
            @params = new { protocolVersion = "2025-03-26", capabilities = new { }, clientInfo = new { name = "test", version = "1" } }
        }));
        Assert.Equal(HttpStatusCode.OK, init.StatusCode);
        Assert.Equal("{{ServerName}}", ParseMessage(await init.Content.ReadAsStringAsync()).GetProperty("result").GetProperty("serverInfo").GetProperty("name").GetString());

        var list = await client.PostAsync("/mcp", Rpc(new { jsonrpc = "2.0", id = 2, method = "tools/list" }));
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.True(ParseMessage(await list.Content.ReadAsStringAsync()).GetProperty("result").TryGetProperty("tools", out _));
    }
}
