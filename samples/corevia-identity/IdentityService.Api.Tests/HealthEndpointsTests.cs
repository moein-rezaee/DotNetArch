using System.Net;
using System.Text.Json;
using IdentityService.Api.Health;
using IdentityService.Infrastructure.Database.Context;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IdentityService.Api.Tests;

/// <summary>
/// Liveness/readiness over a real Kestrel listener on an ephemeral loopback port, using the same
/// <c>AddIdentityHealth</c>/<c>MapIdentityReadiness</c> wiring as Program.cs. "Healthy" is a SQLite in-memory database;
/// "unavailable" is a SQLite file in a directory that does not exist, so EF CanConnect genuinely fails.
/// </summary>
public sealed class HealthEndpointsTests : IAsyncLifetime
{
    private readonly List<WebApplication> _apps = new();
    private SqliteConnection? _memory;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var app in _apps)
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }

        _memory?.Dispose();
    }

    private async Task<HttpClient> StartAsync(Action<DbContextOptionsBuilder> configureDb)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddDbContext<IdentityDbContext>(configureDb);
        builder.Services.AddIdentityHealth();

        var app = builder.Build();
        app.MapGet("/health", HealthEndpoints.Liveness); // same mapping as Program.cs
        app.MapIdentityReadiness();
        await app.StartAsync();
        _apps.Add(app);

        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        return new HttpClient { BaseAddress = new Uri(address) };
    }

    private Task<HttpClient> StartHealthyAsync()
    {
        _memory = new SqliteConnection("DataSource=:memory:");
        _memory.Open();
        return StartAsync(o => o.UseSqlite(_memory));
    }

    private Task<HttpClient> StartUnavailableAsync()
        => StartAsync(o => o.UseSqlite("Data Source=/corevia-nonexistent-dir/identity.db;Mode=ReadWrite"));

    [Fact]
    public async Task Ready_returns_200_when_the_database_is_reachable()
    {
        using var client = await StartHealthyAsync();

        var response = await client.GetAsync("/health/ready");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("healthy", json.RootElement.GetProperty("status").GetString());
        Assert.Equal("healthy", json.RootElement.GetProperty("checks").GetProperty("database").GetString());
    }

    [Fact]
    public async Task Ready_returns_503_when_the_database_check_fails()
    {
        using var client = await StartUnavailableAsync();

        var response = await client.GetAsync("/health/ready");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("unhealthy", json.RootElement.GetProperty("status").GetString());
        Assert.Equal("unhealthy", json.RootElement.GetProperty("checks").GetProperty("database").GetString());
    }

    [Fact]
    public async Task Liveness_stays_cheap_and_returns_200_even_when_the_database_is_down()
    {
        using var client = await StartUnavailableAsync();

        var response = await client.GetAsync("/health");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("healthy", json.RootElement.GetProperty("status").GetString());
        Assert.True(json.RootElement.TryGetProperty("timestamp", out _));
    }
}
