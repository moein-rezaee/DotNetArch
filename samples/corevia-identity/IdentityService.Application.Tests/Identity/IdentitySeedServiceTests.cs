using System.Security.Cryptography;
using System.Text;
using IdentityService.Application.Features.Identity.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace IdentityService.Application.Tests.Identity;

public sealed class IdentitySeedServiceTests : IDisposable
{
    private readonly TestDb _db = TestDb.Create();

    public void Dispose() => _db.Dispose();

    private IdentitySeedService Seed(IdentityClientOptions options)
        => new(_db.Uow, Options.Create(options), NullLogger<IdentitySeedService>.Instance);

    private static string Hash(string value) => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    [Fact]
    public async Task Seeding_is_idempotent()
    {
        var options = new IdentityClientOptions { DefaultPublicClientId = "web-public" };
        await Seed(options).SeedAsync();
        var counts = await CountsAsync();

        await Seed(options).SeedAsync();

        Assert.Equal(counts, await CountsAsync());
        Assert.All(new[] { counts.Roles, counts.Permissions, counts.Scopes }, c => Assert.True(c > 0));
    }

    private async Task<(int Roles, int Permissions, int Scopes, int Clients, int ClientScopes)> CountsAsync()
    {
        using var c = _db.NewContext();
        return (await c.Roles.CountAsync(), await c.Permissions.CountAsync(), await c.Scopes.CountAsync(),
            await c.Clients.CountAsync(), await c.ClientScopes.CountAsync());
    }

    [Fact]
    public async Task Public_client_is_seeded_with_the_self_service_scopes_only()
    {
        await Seed(new IdentityClientOptions { DefaultPublicClientId = " web-public " }).SeedAsync();

        using var c = _db.NewContext();
        var client = await c.Clients.SingleAsync();
        Assert.Equal("web-public", client.ClientId);
        var scopeNames = await (from cs in c.ClientScopes join s in c.Scopes on cs.ScopeId equals s.Id select s.Name).ToListAsync();
        Assert.Contains("identity.self", scopeNames);
        Assert.DoesNotContain("identity.mcp.admin.write", scopeNames);
    }

    [Fact]
    public async Task M2M_client_secret_is_stored_hashed_and_scopes_assigned()
    {
        var options = new IdentityClientOptions
        {
            M2MClients = new Dictionary<string, M2MClientConfig>
            {
                ["svc-a"] = new() { Name = "Svc A", Secret = "plain-secret-a", Scopes = new[] { "identity.self" } }
            }
        };

        await Seed(options).SeedAsync();

        using var c = _db.NewContext();
        var secret = await c.ClientSecrets.SingleAsync();
        Assert.Equal(Hash("plain-secret-a"), secret.Hash);
        Assert.NotEqual("plain-secret-a", secret.Hash);
        Assert.Single(await c.ClientScopes.ToListAsync());
    }

    [Fact]
    public async Task Rotating_an_M2M_secret_revokes_the_previous_one()
    {
        M2MClientConfig Config(string secret) => new() { Name = "Svc A", Secret = secret, Scopes = new[] { "identity.self" } };
        await Seed(new IdentityClientOptions { M2MClients = new() { ["svc-a"] = Config("first") } }).SeedAsync();

        await Seed(new IdentityClientOptions { M2MClients = new() { ["svc-a"] = Config("second") } }).SeedAsync();

        using var c = _db.NewContext();
        var secrets = await c.ClientSecrets.ToListAsync();
        Assert.Equal(2, secrets.Count);
        Assert.NotNull(secrets.Single(s => s.Hash == Hash("first")).RevokedAt);
        Assert.Null(secrets.Single(s => s.Hash == Hash("second")).RevokedAt);
    }

    [Fact]
    public async Task Optional_client_without_a_secret_is_skipped_but_required_one_fails_startup()
    {
        var optional = new IdentityClientOptions
        {
            M2MClients = new() { ["svc-opt"] = new() { Name = "Opt", Optional = true, RequiredSecretKey = "M2M_OPT_SECRET" } }
        };
        await Seed(optional).SeedAsync();
        Assert.Empty(await _db.NewContext().Clients.ToListAsync());

        var required = new IdentityClientOptions
        {
            M2MClients = new() { ["svc-req"] = new() { Name = "Req", RequiredSecretKey = "M2M_REQ_SECRET" } }
        };
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Seed(required).SeedAsync());
        Assert.Contains("M2M_REQ_SECRET", ex.Message);
        Assert.DoesNotContain("plain", ex.Message);
    }

    [Fact]
    public async Task Required_client_with_unknown_scopes_fails_startup()
    {
        var options = new IdentityClientOptions
        {
            M2MClients = new()
            {
                ["svc-req"] = new() { Name = "Req", Secret = "s", RequiredSecretKey = "M2M_REQ_SECRET", Scopes = new[] { "no.such.scope" } }
            }
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Seed(options).SeedAsync());

        Assert.Contains("no.such.scope", ex.Message);
    }
}
