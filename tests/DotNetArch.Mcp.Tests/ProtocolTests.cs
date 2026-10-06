using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace DotNetArch.Mcp.Tests;

/// <summary>Starts the real <c>dotnet-arch mcp serve</c> process and talks MCP over stdio. Needs the built CLI; run by smoke.sh.</summary>
[Trait("Category", "Integration")]
public class ProtocolTests
{
    private static string CliPath()
    {
        // .../tests/DotNetArch.Mcp.Tests/bin/<Configuration>/<tfm>/ -> .../src/DotNetArch.Cli/bin/<Configuration>/<tfm>/DotNetArch.dll
        var baseDirectory = new DirectoryInfo(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
        var tfm = baseDirectory.Name;
        var configuration = baseDirectory.Parent!.Name;
        var repository = baseDirectory.Parent.Parent!.Parent!.Parent!.Parent!.FullName;
        return Path.Combine(repository, "src", "DotNetArch.Cli", "bin", configuration, tfm, "DotNetArch.dll");
    }

    [Fact]
    public async Task The_server_initialises_lists_tools_and_answers_a_call()
    {
        var startInfo = new ProcessStartInfo("dotnet", $"\"{CliPath()}\" mcp serve")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        using var process = Process.Start(startInfo)!;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        async Task<JsonElement> Exchange(object request)
        {
            await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(request));
            await process.StandardInput.FlushAsync(timeout.Token);
            var line = await process.StandardOutput.ReadLineAsync(timeout.Token);
            return JsonDocument.Parse(line!).RootElement.Clone();
        }

        var init = await Exchange(new { jsonrpc = "2.0", id = 1, method = "initialize", @params = new { protocolVersion = "2025-03-26", capabilities = new { }, clientInfo = new { name = "test", version = "1" } } });
        Assert.Equal("dotnet-arch", init.GetProperty("result").GetProperty("serverInfo").GetProperty("name").GetString());

        await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new { jsonrpc = "2.0", method = "notifications/initialized" }));
        var tools = await Exchange(new { jsonrpc = "2.0", id = 2, method = "tools/list" });
        var names = tools.GetProperty("result").GetProperty("tools").EnumerateArray().Select(tool => tool.GetProperty("name").GetString()).ToList();
        Assert.Contains("new_solution", names);
        Assert.Equal(15, names.Count);

        var call = await Exchange(new { jsonrpc = "2.0", id = 3, method = "tools/call", @params = new { name = "describe_config", arguments = new { solutionPath = Path.GetTempPath() + "/does-not-exist" } } });
        var text = call.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString()!;
        Assert.Contains("\"ok\":false", text);

        process.StandardInput.Close();
        await process.WaitForExitAsync(timeout.Token);
    }
}
