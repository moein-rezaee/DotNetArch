using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;
{{ControllerUsing}}

namespace {{App}}.Api.Tests;

/// <summary>
/// The public contract: the OpenAPI document of the controllers equals the snapshot committed in docs/api/openapi.json, so a change of a route, a
/// parameter or a result is a reviewed change. This service has no in-process test host of the whole application, so the document is made by a small
/// host over the same controllers (routes, parameters, results). Write the snapshot with <c>UPDATE_OPENAPI_SNAPSHOT=1 dotnet test</c>.
/// </summary>
public class OpenApiSnapshotTests
{
    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };

    [Fact]
    public async Task The_openapi_document_matches_the_committed_snapshot()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddControllers().AddApplicationPart(typeof({{Controller}}).Assembly);
        builder.Services.AddSwaggerGen();
        await using var app = builder.Build();
        app.UseSwagger();
        app.MapControllers();
        await app.StartAsync();

        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        using var client = new HttpClient { BaseAddress = new Uri(address) };
        var response = await client.GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();
        var current = Normalize(await response.Content.ReadAsStringAsync());
        var snapshot = Path.Combine(SolutionRoot(), "docs", "api", "openapi.json");

        if (Environment.GetEnvironmentVariable("UPDATE_OPENAPI_SNAPSHOT") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(snapshot)!);
            File.WriteAllText(snapshot, current);
            return;
        }

        Assert.True(File.Exists(snapshot), "docs/api/openapi.json is missing. Write it with UPDATE_OPENAPI_SNAPSHOT=1 dotnet test.");
        Assert.Equal(Normalize(File.ReadAllText(snapshot)), current);
    }

    private static string Normalize(string json) =>
        JsonSerializer.Serialize(JsonDocument.Parse(json).RootElement, Pretty).ReplaceLineEndings("\n") + "\n";

    private static string SolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "dotnet-arch.yml")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("dotnet-arch.yml was not found above the test output.");
    }
}
