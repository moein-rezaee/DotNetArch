using System.Security.Cryptography;
using System.Text;

namespace IdentityService.Application.Tests.Management;

public sealed class ScopeClientTenantHandlerTests : IDisposable
{
    private readonly TestDb _db = TestDb.Create();

    public void Dispose() => _db.Dispose();

    // ---- scopes
    [Fact]
    public async Task CreateScope_trims_and_rejects_duplicates()
    {
        var handler = new CreateScopeCommandHandler(_db.Uow);

        var dto = await handler.Handle(new CreateScopeCommand(new CreateScopeRequest(" invoice.read ", " Invoice read ", null)), default);

        Assert.Equal("invoice.read", dto.Name);
        Assert.Equal("duplicate_scope_name", (await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(new CreateScopeCommand(new CreateScopeRequest("invoice.read", "x", null)), default))).ErrorCode);
    }

    [Fact]
    public async Task UpdateScope_updates_blocks_duplicates_and_404s()
    {
        var scope = await Builders.AddScopeAsync(_db.Uow, "a.read");
        await Builders.AddScopeAsync(_db.Uow, "taken");
        var handler = new UpdateScopeCommandHandler(_db.Uow);

        var dto = await handler.Handle(new UpdateScopeCommand(scope.Id, new UpdateScopeRequest("a.read2", "A", "d")), default);

        Assert.Equal("a.read2", dto.Name);
        Assert.Equal("duplicate_scope_name", (await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(new UpdateScopeCommand(scope.Id, new UpdateScopeRequest("taken", "x", null)), default))).ErrorCode);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new UpdateScopeCommand(Guid.NewGuid(), new UpdateScopeRequest("n", "d", null)), default));
    }

    [Fact]
    public async Task DeleteScope_removes_scope_and_permission_links_but_refuses_when_assigned_to_a_client()
    {
        var free = await Builders.AddScopeAsync(_db.Uow, "free");
        var inUse = await Builders.AddScopeAsync(_db.Uow, "in.use");
        var client = await Builders.AddClientAsync(_db.Uow, "c1");
        var permission = await Builders.AddPermissionAsync(_db.Uow, "Identity.A");
        await Builders.LinkClientScopeAsync(_db.Uow, client.Id, inUse.Id);
        await _db.Uow.Repository<ScopePermission>().AddAsync(new ScopePermission { ScopeId = free.Id, PermissionId = permission.Id });
        await _db.Uow.SaveChangesAsync();
        var handler = new DeleteScopeCommandHandler(_db.Uow);

        await handler.Handle(new DeleteScopeCommand(free.Id), default);

        using var verify = _db.NewContext();
        Assert.DoesNotContain(await verify.Scopes.ToListAsync(), s => s.Id == free.Id);
        Assert.Empty(await verify.ScopePermissions.ToListAsync());
        Assert.Equal("scope_has_clients", (await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(new DeleteScopeCommand(inUse.Id), default))).ErrorCode);
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new DeleteScopeCommand(Guid.NewGuid()), default));
    }

    [Fact]
    public async Task GetScopeById_includes_permissions_and_GetScopesPaged_orders_by_name()
    {
        var scope = await Builders.AddScopeAsync(_db.Uow, "b.scope");
        await Builders.AddScopeAsync(_db.Uow, "a.scope");
        var permission = await Builders.AddPermissionAsync(_db.Uow, "Identity.A");
        await _db.Uow.Repository<ScopePermission>().AddAsync(new ScopePermission { ScopeId = scope.Id, PermissionId = permission.Id });
        await _db.Uow.SaveChangesAsync();

        var dto = await new GetScopeByIdQueryHandler(_db.Uow).Handle(new GetScopeByIdQuery(scope.Id), default);
        var page = await new GetScopesPagedQueryHandler(_db.Uow).Handle(new GetScopesPagedQuery(1, 10), default);

        Assert.Equal("Identity.A", Assert.Single(dto.Permissions).Key);
        Assert.Equal(new[] { "a.scope", "b.scope" }, page.Items.Select(i => i.Name).ToArray());
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetScopeByIdQueryHandler(_db.Uow).Handle(new GetScopeByIdQuery(Guid.NewGuid()), default));
    }

    // ---- clients
    [Fact]
    public async Task CreateClient_generates_a_unique_public_client_id()
    {
        var handler = new CreateClientCommandHandler(_db.Uow);

        var a = await handler.Handle(new CreateClientCommand(new CreateClientRequest(" Web ", "d", true)), default);
        var b = await handler.Handle(new CreateClientCommand(new CreateClientRequest("Web2", null, false)), default);

        Assert.Equal("Web", a.Name);
        Assert.NotEqual(a.ClientId, b.ClientId);
        Assert.Equal(32, a.ClientId.Length);
        Assert.False(b.IsActive);
    }

    [Fact]
    public async Task UpdateClient_updates_and_404s()
    {
        var client = await Builders.AddClientAsync(_db.Uow, "c1");
        var handler = new UpdateClientCommandHandler(_db.Uow);

        var dto = await handler.Handle(new UpdateClientCommand(client.Id, new UpdateClientRequest("New", "d", false)), default);

        Assert.Equal("New", dto.Name);
        Assert.False(dto.IsActive);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new UpdateClientCommand(Guid.NewGuid(), new UpdateClientRequest("n", null, true)), default));
    }

    [Fact]
    public async Task CreateClientSecret_returns_the_plain_secret_once_and_stores_only_its_sha256_hash()
    {
        var client = await Builders.AddClientAsync(_db.Uow, "c1");

        var response = await new CreateClientSecretCommandHandler(_db.Uow)
            .Handle(new CreateClientSecretCommand(client.Id, new CreateClientSecretRequest(null)), default);

        var stored = await _db.NewContext().ClientSecrets.SingleAsync();
        Assert.NotEqual(response.Secret, stored.Hash);
        Assert.Equal(Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(response.Secret))), stored.Hash);
        Assert.Equal(32, Convert.FromBase64String(response.Secret).Length);
        Assert.Null(stored.ExpiresAt);
        await Assert.ThrowsAsync<NotFoundException>(() => new CreateClientSecretCommandHandler(_db.Uow)
            .Handle(new CreateClientSecretCommand(Guid.NewGuid(), new CreateClientSecretRequest(null)), default));
    }

    [Fact]
    public async Task GetClientSecrets_never_returns_secret_material()
    {
        var client = await Builders.AddClientAsync(_db.Uow, "c1");
        await new CreateClientSecretCommandHandler(_db.Uow)
            .Handle(new CreateClientSecretCommand(client.Id, new CreateClientSecretRequest(DateTime.UtcNow.AddDays(1))), default);

        var list = await new GetClientSecretsQueryHandler(_db.Uow).Handle(new GetClientSecretsQuery(client.Id), default);

        Assert.Equal(string.Empty, Assert.Single(list).Secret);
        Assert.NotNull(list.Single().ExpiresAt);
    }

    [Fact]
    public async Task RevokeClientSecret_sets_RevokedAt_once_and_404s_for_wrong_client_or_secret()
    {
        var client = await Builders.AddClientAsync(_db.Uow, "c1");
        var other = await Builders.AddClientAsync(_db.Uow, "c2");
        var secret = await new CreateClientSecretCommandHandler(_db.Uow)
            .Handle(new CreateClientSecretCommand(client.Id, new CreateClientSecretRequest(null)), default);
        var handler = new RevokeClientSecretCommandHandler(_db.Uow);

        await handler.Handle(new RevokeClientSecretCommand(client.Id, secret.Id), default);
        var first = (await _db.NewContext().ClientSecrets.SingleAsync()).RevokedAt;
        await handler.Handle(new RevokeClientSecretCommand(client.Id, secret.Id), default);

        Assert.NotNull(first);
        Assert.Equal(first, (await _db.NewContext().ClientSecrets.SingleAsync()).RevokedAt);
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new RevokeClientSecretCommand(other.Id, secret.Id), default));
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new RevokeClientSecretCommand(client.Id, Guid.NewGuid()), default));
    }

    [Fact]
    public async Task GetClientById_includes_scopes_and_GetClientsPaged_orders_by_name()
    {
        var client = await Builders.AddClientAsync(_db.Uow, "z-client");
        await Builders.AddClientAsync(_db.Uow, "a-client");
        var scope = await Builders.AddScopeAsync(_db.Uow, "x.read");
        await Builders.LinkClientScopeAsync(_db.Uow, client.Id, scope.Id);

        var dto = await new GetClientByIdQueryHandler(_db.Uow).Handle(new GetClientByIdQuery(client.Id), default);
        var page = await new GetClientsPagedQueryHandler(_db.Uow).Handle(new GetClientsPagedQuery(1, 10), default);

        Assert.Equal("x.read", Assert.Single(dto.Scopes).Name);
        Assert.Equal(new[] { "a-client", "z-client" }, page.Items.Select(i => i.Name).ToArray());
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new GetClientByIdQueryHandler(_db.Uow).Handle(new GetClientByIdQuery(Guid.NewGuid()), default));
    }

    // ---- tenants
    [Fact]
    public async Task Tenant_create_update_get_and_page()
    {
        var created = await new CreateTenantCommandHandler(_db.Uow)
            .Handle(new CreateTenantCommand(new CreateTenantRequest(" acme ", " Acme ", true)), default);
        await Builders.AddTenantAsync(_db.Uow, "aaa");

        var updated = await new UpdateTenantCommandHandler(_db.Uow)
            .Handle(new UpdateTenantCommand(created.Id, new UpdateTenantRequest("acme2", "Acme Two", false)), default);
        var fetched = await new GetTenantByIdQueryHandler(_db.Uow).Handle(new GetTenantByIdQuery(created.Id), default);
        var page = await new GetTenantsPagedQueryHandler(_db.Uow).Handle(new GetTenantsPagedQuery(1, 10), default);

        Assert.Equal("acme", created.Name);
        Assert.Equal("acme2", updated.Name);
        Assert.False(fetched.IsActive);
        Assert.Equal(new[] { "aaa", "acme2" }, page.Items.Select(i => i.Name).ToArray());
    }

    [Fact]
    public async Task Tenant_error_paths_have_precise_types_and_codes()
    {
        var t = await Builders.AddTenantAsync(_db.Uow, "acme");
        await Builders.AddTenantAsync(_db.Uow, "other");
        var user = await Builders.AddUserAsync(_db.Uow);
        await Builders.LinkUserTenantAsync(_db.Uow, user.Id, t.Id);

        Assert.Equal("duplicate_tenant_name", (await Assert.ThrowsAsync<BadRequestException>(() => new CreateTenantCommandHandler(_db.Uow)
            .Handle(new CreateTenantCommand(new CreateTenantRequest("acme", "x", true)), default))).ErrorCode);
        Assert.Equal("duplicate_tenant_name", (await Assert.ThrowsAsync<BadRequestException>(() => new UpdateTenantCommandHandler(_db.Uow)
            .Handle(new UpdateTenantCommand(t.Id, new UpdateTenantRequest("other", "x", true)), default))).ErrorCode);
        await Assert.ThrowsAsync<NotFoundException>(() => new UpdateTenantCommandHandler(_db.Uow)
            .Handle(new UpdateTenantCommand(Guid.NewGuid(), new UpdateTenantRequest("n", "d", true)), default));
        await Assert.ThrowsAsync<NotFoundException>(() => new GetTenantByIdQueryHandler(_db.Uow)
            .Handle(new GetTenantByIdQuery(Guid.NewGuid()), default));
        await Assert.ThrowsAsync<NotFoundException>(() => new DeleteTenantCommandHandler(_db.Uow)
            .Handle(new DeleteTenantCommand(Guid.NewGuid()), default));
        Assert.Equal("tenant_has_users", (await Assert.ThrowsAsync<BadRequestException>(() => new DeleteTenantCommandHandler(_db.Uow)
            .Handle(new DeleteTenantCommand(t.Id), default))).ErrorCode);
    }

    [Fact]
    public async Task DeleteTenant_without_members_removes_it()
    {
        var t = await Builders.AddTenantAsync(_db.Uow, "solo");

        await new DeleteTenantCommandHandler(_db.Uow).Handle(new DeleteTenantCommand(t.Id), default);

        Assert.Empty(await _db.NewContext().Tenants.ToListAsync());
    }
}
