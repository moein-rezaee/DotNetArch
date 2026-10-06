namespace IdentityService.Application.Tests.Management;

public sealed class SessionProfileRelationHandlerTests : IDisposable
{
    private readonly TestDb _db = TestDb.Create();

    public void Dispose() => _db.Dispose();

    private async Task<(UserSession Session, RefreshToken Token)> AddSessionAsync(User user, bool revoked = false, DateTime? tokenExpires = null)
    {
        var session = new UserSession { Id = Guid.NewGuid(), UserId = user.Id, CreatedAt = DateTime.UtcNow, IsRevoked = revoked };
        var token = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTime.UtcNow,
            UserSessionId = session.Id,
            ExpiresAt = tokenExpires
        };
        await _db.Uow.Repository<UserSession>().AddAsync(session);
        await _db.Uow.Repository<RefreshToken>().AddAsync(token);
        await _db.Uow.SaveChangesAsync();
        return (session, token);
    }

    // ---- sessions
    [Fact]
    public async Task GetCurrentUserSessions_returns_only_the_users_sessions_newest_first()
    {
        var user = await Builders.AddUserAsync(_db.Uow, "09120000001");
        var other = await Builders.AddUserAsync(_db.Uow, "09120000002");
        var (older, _) = await AddSessionAsync(user);
        await Task.Delay(5);
        var (newer, _) = await AddSessionAsync(user);
        await AddSessionAsync(other);

        var sessions = await new GetCurrentUserSessionsQueryHandler(_db.Uow)
            .Handle(new GetCurrentUserSessionsQuery(user.Id), default);

        Assert.Equal(new[] { newer.Id, older.Id }, sessions.Select(s => s.Id).ToArray());
    }

    [Fact]
    public async Task RevokeSession_revokes_the_session_and_its_live_refresh_tokens()
    {
        var user = await Builders.AddUserAsync(_db.Uow);
        var (session, token) = await AddSessionAsync(user);

        await new RevokeSessionCommandHandler(_db.Uow).Handle(new RevokeSessionCommand(user.Id, session.Id), default);

        using var verify = _db.NewContext();
        var stored = await verify.UserSessions.SingleAsync();
        Assert.True(stored.IsRevoked);
        Assert.NotNull(stored.EndedAt);
        Assert.NotNull((await verify.RefreshTokens.SingleAsync(t => t.Id == token.Id)).RevokedAt);
    }

    [Fact]
    public async Task RevokeSession_of_another_users_or_unknown_session_is_not_found()
    {
        var user = await Builders.AddUserAsync(_db.Uow, "09120000001");
        var other = await Builders.AddUserAsync(_db.Uow, "09120000002");
        var (session, _) = await AddSessionAsync(other);
        var handler = new RevokeSessionCommandHandler(_db.Uow);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new RevokeSessionCommand(user.Id, session.Id), default));
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new RevokeSessionCommand(user.Id, Guid.NewGuid()), default));
    }

    [Fact]
    public async Task RevokeOtherSessions_keeps_the_current_session_alive()
    {
        var user = await Builders.AddUserAsync(_db.Uow);
        var (current, currentToken) = await AddSessionAsync(user);
        var (other, otherToken) = await AddSessionAsync(user);

        await new RevokeOtherSessionsCommandHandler(_db.Uow).Handle(new RevokeOtherSessionsCommand(user.Id, current.Id), default);

        using var verify = _db.NewContext();
        Assert.False((await verify.UserSessions.SingleAsync(s => s.Id == current.Id)).IsRevoked);
        Assert.Null((await verify.RefreshTokens.SingleAsync(t => t.Id == currentToken.Id)).RevokedAt);
        Assert.True((await verify.UserSessions.SingleAsync(s => s.Id == other.Id)).IsRevoked);
        Assert.NotNull((await verify.RefreshTokens.SingleAsync(t => t.Id == otherToken.Id)).RevokedAt);
    }

    [Fact]
    public async Task RevokeOtherSessions_without_a_current_session_revokes_everything_for_that_user_only()
    {
        var user = await Builders.AddUserAsync(_db.Uow, "09120000001");
        var stranger = await Builders.AddUserAsync(_db.Uow, "09120000002");
        await AddSessionAsync(user);
        await AddSessionAsync(user);
        var (strangerSession, _) = await AddSessionAsync(stranger);

        await new RevokeOtherSessionsCommandHandler(_db.Uow).Handle(new RevokeOtherSessionsCommand(user.Id, null), default);

        using var verify = _db.NewContext();
        Assert.Equal(2, await verify.UserSessions.CountAsync(s => s.IsRevoked));
        Assert.False((await verify.UserSessions.SingleAsync(s => s.Id == strangerSession.Id)).IsRevoked);
    }

    // ---- profile
    [Fact]
    public async Task Profile_is_created_on_first_update_trimmed_and_returned()
    {
        var user = await Builders.AddUserAsync(_db.Uow);
        var empty = await new GetProfileQueryHandler(_db.Uow).Handle(new GetProfileQuery(user.Id), default);
        Assert.Null(empty.FirstName);

        await new UpdateProfileCommandHandler(_db.Uow).Handle(new UpdateProfileCommand(user.Id,
            new UpdateProfileRequest(" Ada ", " Lovelace ", " ada@example.test ", null)), default);
        await new UpdateProfileCommandHandler(_db.Uow).Handle(new UpdateProfileCommand(user.Id,
            new UpdateProfileRequest("Ada", "L.", "ada@example.test", "https://img.test/a.png")), default);

        var profile = await new GetProfileQueryHandler(_db.Uow).Handle(new GetProfileQuery(user.Id), default);
        Assert.Equal("Ada", profile.FirstName);
        Assert.Equal("L.", profile.LastName);
        Assert.Equal("https://img.test/a.png", profile.AvatarUrl);
        Assert.Equal(1, await _db.NewContext().UserProfiles.CountAsync());
    }

    [Fact]
    public async Task Profile_for_an_unknown_user_is_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => new GetProfileQueryHandler(_db.Uow).Handle(new GetProfileQuery(Guid.NewGuid()), default));
        await Assert.ThrowsAsync<NotFoundException>(() => new UpdateProfileCommandHandler(_db.Uow)
            .Handle(new UpdateProfileCommand(Guid.NewGuid(), new UpdateProfileRequest(null, null, null, null)), default));
    }

    // ---- user roles
    [Fact]
    public async Task UserRole_add_is_idempotent_list_and_remove()
    {
        var user = await Builders.AddUserAsync(_db.Uow);
        var role = await Builders.AddRoleAsync(_db.Uow, "Support");
        var add = new AddUserRoleCommandHandler(_db.Uow, Options.Create(new RootAdminOptions()));

        await add.Handle(new AddUserRoleCommand(user.Id, new UserRoleRequest(role.Id)), default);
        var after = await add.Handle(new AddUserRoleCommand(user.Id, new UserRoleRequest(role.Id)), default);
        var listed = await new GetUserRolesQueryHandler(_db.Uow).Handle(new GetUserRolesQuery(user.Id), default);

        Assert.Single(after);
        Assert.Equal("Support", Assert.Single(listed).Role.Name);

        await new RemoveUserRoleCommandHandler(_db.Uow, Options.Create(new RootAdminOptions()))
            .Handle(new RemoveUserRoleCommand(user.Id, role.Id), default);
        Assert.Empty(await _db.NewContext().UserRoles.ToListAsync());
    }

    [Fact]
    public async Task UserRole_add_not_found_paths_and_SuperAdmin_guard()
    {
        var user = await Builders.AddUserAsync(_db.Uow, "09120000001");
        var superAdmin = await Builders.AddRoleAsync(_db.Uow, "SuperAdmin");
        var add = new AddUserRoleCommandHandler(_db.Uow, Options.Create(new RootAdminOptions { PhoneNumber = "09129999999" }));

        await Assert.ThrowsAsync<NotFoundException>(() => add.Handle(new AddUserRoleCommand(Guid.NewGuid(), new UserRoleRequest(superAdmin.Id)), default));
        await Assert.ThrowsAsync<NotFoundException>(() => add.Handle(new AddUserRoleCommand(user.Id, new UserRoleRequest(Guid.NewGuid())), default));
        Assert.Equal("superadmin_assignment_forbidden", (await Assert.ThrowsAsync<BadRequestException>(() =>
            add.Handle(new AddUserRoleCommand(user.Id, new UserRoleRequest(superAdmin.Id)), default))).ErrorCode);

        var root = await Builders.AddUserAsync(_db.Uow, "09129999999");
        var granted = await add.Handle(new AddUserRoleCommand(root.Id, new UserRoleRequest(superAdmin.Id)), default);
        Assert.Equal("SuperAdmin", Assert.Single(granted).Role.Name);
    }

    [Fact]
    public async Task SuperAdmin_cannot_be_removed_from_the_root_admin()
    {
        var root = await Builders.AddUserAsync(_db.Uow, "09129999999");
        var superAdmin = await Builders.AddRoleAsync(_db.Uow, "SuperAdmin");
        await Builders.LinkUserRoleAsync(_db.Uow, root.Id, superAdmin.Id);
        var remove = new RemoveUserRoleCommandHandler(_db.Uow, Options.Create(new RootAdminOptions { PhoneNumber = "09129999999" }));

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => remove.Handle(new RemoveUserRoleCommand(root.Id, superAdmin.Id), default));

        Assert.Equal("superadmin_unassign_forbidden", ex.ErrorCode);
        Assert.Single(await _db.NewContext().UserRoles.ToListAsync());
    }

    // ---- user tenants
    [Fact]
    public async Task UserTenant_add_default_switches_other_defaults_off_and_remove_deletes_link()
    {
        var user = await Builders.AddUserAsync(_db.Uow);
        var a = await Builders.AddTenantAsync(_db.Uow, "a");
        var b = await Builders.AddTenantAsync(_db.Uow, "b");
        var add = new AddUserTenantCommandHandler(_db.Uow);

        await add.Handle(new AddUserTenantCommand(user.Id, new UserTenantRequest(a.Id, true)), default);
        var result = await add.Handle(new AddUserTenantCommand(user.Id, new UserTenantRequest(b.Id, true)), default);

        Assert.Equal(b.Id, Assert.Single(result, r => r.IsDefault).Tenant.Id);
        Assert.Equal(2, (await new GetUserTenantsQueryHandler(_db.Uow).Handle(new GetUserTenantsQuery(user.Id), default)).Count);

        await new RemoveUserTenantCommandHandler(_db.Uow).Handle(new RemoveUserTenantCommand(user.Id, a.Id), default);
        Assert.Single(await _db.NewContext().UserTenants.ToListAsync());
    }

    [Fact]
    public async Task UserTenant_add_existing_link_only_ever_promotes_to_default_and_missing_entities_404()
    {
        var user = await Builders.AddUserAsync(_db.Uow);
        var a = await Builders.AddTenantAsync(_db.Uow, "a");
        var add = new AddUserTenantCommandHandler(_db.Uow);
        await add.Handle(new AddUserTenantCommand(user.Id, new UserTenantRequest(a.Id, true)), default);

        var again = await add.Handle(new AddUserTenantCommand(user.Id, new UserTenantRequest(a.Id, false)), default);

        Assert.True(Assert.Single(again).IsDefault);
        await Assert.ThrowsAsync<NotFoundException>(() => add.Handle(new AddUserTenantCommand(Guid.NewGuid(), new UserTenantRequest(a.Id, false)), default));
        await Assert.ThrowsAsync<NotFoundException>(() => add.Handle(new AddUserTenantCommand(user.Id, new UserTenantRequest(Guid.NewGuid(), false)), default));
    }

    // ---- client scopes / role permissions / scope permissions
    [Fact]
    public async Task ClientScope_add_is_idempotent_list_remove_and_404s()
    {
        var client = await Builders.AddClientAsync(_db.Uow, "c1");
        var scope = await Builders.AddScopeAsync(_db.Uow, "x.read");
        var add = new AddClientScopeCommandHandler(_db.Uow);

        await add.Handle(new AddClientScopeCommand(client.Id, new ClientScopeRequest(scope.Id)), default);
        var after = await add.Handle(new AddClientScopeCommand(client.Id, new ClientScopeRequest(scope.Id)), default);
        var listed = await new GetClientScopesQueryHandler(_db.Uow).Handle(new GetClientScopesQuery(client.Id), default);

        Assert.Single(after);
        Assert.Equal("x.read", Assert.Single(listed).Scope.Name);
        await Assert.ThrowsAsync<NotFoundException>(() => add.Handle(new AddClientScopeCommand(Guid.NewGuid(), new ClientScopeRequest(scope.Id)), default));
        await Assert.ThrowsAsync<NotFoundException>(() => add.Handle(new AddClientScopeCommand(client.Id, new ClientScopeRequest(Guid.NewGuid())), default));

        await new RemoveClientScopeCommandHandler(_db.Uow).Handle(new RemoveClientScopeCommand(client.Id, scope.Id), default);
        Assert.Empty(await _db.NewContext().ClientScopes.ToListAsync());
    }

    [Fact]
    public async Task RolePermission_add_is_idempotent_list_remove_and_404s()
    {
        var role = await Builders.AddRoleAsync(_db.Uow, "Support");
        var permission = await Builders.AddPermissionAsync(_db.Uow, "Identity.A");
        var add = new AddRolePermissionCommandHandler(_db.Uow);

        await add.Handle(new AddRolePermissionCommand(role.Id, new RolePermissionRequest(permission.Id)), default);
        var after = await add.Handle(new AddRolePermissionCommand(role.Id, new RolePermissionRequest(permission.Id)), default);
        var listed = await new GetRolePermissionsQueryHandler(_db.Uow).Handle(new GetRolePermissionsQuery(role.Id), default);

        Assert.Single(after);
        Assert.Equal("Identity.A", Assert.Single(listed).Permission.Key);
        await Assert.ThrowsAsync<NotFoundException>(() => add.Handle(new AddRolePermissionCommand(Guid.NewGuid(), new RolePermissionRequest(permission.Id)), default));
        await Assert.ThrowsAsync<NotFoundException>(() => add.Handle(new AddRolePermissionCommand(role.Id, new RolePermissionRequest(Guid.NewGuid())), default));

        await new RemoveRolePermissionCommandHandler(_db.Uow).Handle(new RemoveRolePermissionCommand(role.Id, permission.Id), default);
        Assert.Empty(await _db.NewContext().RolePermissions.ToListAsync());
    }

    [Fact]
    public async Task ScopePermission_add_is_idempotent_list_remove_and_404s()
    {
        var scope = await Builders.AddScopeAsync(_db.Uow, "x.read");
        var permission = await Builders.AddPermissionAsync(_db.Uow, "Identity.A");
        var add = new AddScopePermissionCommandHandler(_db.Uow);

        await add.Handle(new AddScopePermissionCommand(scope.Id, new ScopePermissionRequest(permission.Id)), default);
        var after = await add.Handle(new AddScopePermissionCommand(scope.Id, new ScopePermissionRequest(permission.Id)), default);
        var listed = await new GetScopePermissionsQueryHandler(_db.Uow).Handle(new GetScopePermissionsQuery(scope.Id), default);

        Assert.Single(after);
        Assert.Equal("Identity.A", Assert.Single(listed).Permission.Key);
        await Assert.ThrowsAsync<NotFoundException>(() => add.Handle(new AddScopePermissionCommand(Guid.NewGuid(), new ScopePermissionRequest(permission.Id)), default));
        await Assert.ThrowsAsync<NotFoundException>(() => add.Handle(new AddScopePermissionCommand(scope.Id, new ScopePermissionRequest(Guid.NewGuid())), default));

        await new RemoveScopePermissionCommandHandler(_db.Uow).Handle(new RemoveScopePermissionCommand(scope.Id, permission.Id), default);
        Assert.Empty(await _db.NewContext().ScopePermissions.ToListAsync());
    }
}
