namespace IdentityService.Application.Tests.Management;

public sealed class RoleAndPermissionHandlerTests : IDisposable
{
    private readonly TestDb _db = TestDb.Create();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task CreateRole_trims_and_persists_and_rejects_duplicate_names()
    {
        var handler = new CreateRoleCommandHandler(_db.Uow);

        var dto = await handler.Handle(new CreateRoleCommand(new CreateRoleRequest(" Support ", " Support Agent ", "desc")), default);

        Assert.Equal("Support", dto.Name);
        Assert.Equal("Support Agent", dto.DisplayName);
        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(new CreateRoleCommand(new CreateRoleRequest("Support", "x", null)), default));
        Assert.Equal("duplicate_role_name", ex.ErrorCode);
    }

    [Fact]
    public async Task UpdateRole_updates_fields_and_blocks_duplicates_and_the_SuperAdmin_role()
    {
        var role = await Builders.AddRoleAsync(_db.Uow, "Support");
        await Builders.AddRoleAsync(_db.Uow, "Taken");
        var superAdmin = await Builders.AddRoleAsync(_db.Uow, "SuperAdmin");
        var handler = new UpdateRoleCommandHandler(_db.Uow);

        var dto = await handler.Handle(new UpdateRoleCommand(role.Id, new UpdateRoleRequest("Support2", "Support Two", "d")), default);
        Assert.Equal("Support2", dto.Name);

        var dup = await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(new UpdateRoleCommand(role.Id, new UpdateRoleRequest("Taken", "x", null)), default));
        Assert.Equal("duplicate_role_name", dup.ErrorCode);

        var forbidden = await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(new UpdateRoleCommand(superAdmin.Id, new UpdateRoleRequest("Renamed", "x", null)), default));
        Assert.Equal("superadmin_update_forbidden", forbidden.ErrorCode);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new UpdateRoleCommand(Guid.NewGuid(), new UpdateRoleRequest("n", "d", null)), default));
    }

    [Fact]
    public async Task DeleteRole_removes_the_role_and_its_permission_links()
    {
        var role = await Builders.AddRoleAsync(_db.Uow, "Support");
        var permission = await Builders.AddPermissionAsync(_db.Uow, "Identity.Users.Get");
        await _db.Uow.Repository<RolePermission>().AddAsync(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
        await _db.Uow.SaveChangesAsync();

        await new DeleteRoleCommandHandler(_db.Uow).Handle(new DeleteRoleCommand(role.Id), default);

        using var verify = _db.NewContext();
        Assert.Empty(await verify.Roles.ToListAsync());
        Assert.Empty(await verify.RolePermissions.ToListAsync());
        Assert.Single(await verify.Permissions.ToListAsync());
    }

    [Fact]
    public async Task DeleteRole_error_paths_use_precise_exception_types_and_codes()
    {
        var superAdmin = await Builders.AddRoleAsync(_db.Uow, "SuperAdmin");
        var assigned = await Builders.AddRoleAsync(_db.Uow, "Assigned");
        var user = await Builders.AddUserAsync(_db.Uow);
        await Builders.LinkUserRoleAsync(_db.Uow, user.Id, assigned.Id);
        var handler = new DeleteRoleCommandHandler(_db.Uow);

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new DeleteRoleCommand(Guid.NewGuid()), default));
        Assert.Equal("superadmin_delete_forbidden",
            (await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(new DeleteRoleCommand(superAdmin.Id), default))).ErrorCode);
        Assert.Equal("role_has_users",
            (await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(new DeleteRoleCommand(assigned.Id), default))).ErrorCode);
    }

    [Fact]
    public async Task GetRoleById_returns_permissions_and_404s_when_missing()
    {
        var role = await Builders.AddRoleAsync(_db.Uow, "Support");
        var permission = await Builders.AddPermissionAsync(_db.Uow, "Identity.Users.Get");
        await _db.Uow.Repository<RolePermission>().AddAsync(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
        await _db.Uow.SaveChangesAsync();
        var handler = new GetRoleByIdQueryHandler(_db.Uow);

        var dto = await handler.Handle(new GetRoleByIdQuery(role.Id), default);

        Assert.Equal("Identity.Users.Get", Assert.Single(dto.Permissions).Key);
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new GetRoleByIdQuery(Guid.NewGuid()), default));
    }

    [Fact]
    public async Task GetRolesPaged_orders_by_name_and_pages()
    {
        foreach (var n in new[] { "c", "a", "b" })
        {
            await Builders.AddRoleAsync(_db.Uow, n);
        }

        var page = await new GetRolesPagedQueryHandler(_db.Uow).Handle(new GetRolesPagedQuery(1, 2), default);

        Assert.Equal(3, page.TotalCount);
        Assert.Equal(new[] { "a", "b" }, page.Items.Select(i => i.Name).ToArray());
    }

    [Fact]
    public async Task CreatePermission_trims_and_rejects_duplicate_keys()
    {
        var handler = new CreatePermissionCommandHandler(_db.Uow);

        var dto = await handler.Handle(new CreatePermissionCommand(new CreatePermissionRequest(" Identity.X.Get ", " X ", null)), default);

        Assert.Equal("Identity.X.Get", dto.Key);
        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            handler.Handle(new CreatePermissionCommand(new CreatePermissionRequest("Identity.X.Get", "d", null)), default));
        Assert.Equal("duplicate_permission_key", ex.ErrorCode);
    }

    [Fact]
    public async Task UpdatePermission_updates_deprecation_and_blocks_key_collisions()
    {
        var permission = await Builders.AddPermissionAsync(_db.Uow, "Identity.A");
        await Builders.AddPermissionAsync(_db.Uow, "Identity.B");
        var handler = new UpdatePermissionCommandHandler(_db.Uow);

        await handler.Handle(new UpdatePermissionCommand(permission.Id,
            new UpdatePermissionRequest("Identity.A2", "A2", "d", true, "legacy")), default);
        var stored = await _db.NewContext().Permissions.SingleAsync(p => p.Id == permission.Id);
        Assert.Equal("Identity.A2", stored.Key);
        Assert.True(stored.IsDeprecated);
        Assert.Equal("legacy", stored.DeprecationReason);

        await handler.Handle(new UpdatePermissionCommand(permission.Id,
            new UpdatePermissionRequest("Identity.A2", "A2", "d", false, "ignored")), default);
        stored = await _db.NewContext().Permissions.SingleAsync(p => p.Id == permission.Id);
        Assert.False(stored.IsDeprecated);
        Assert.Null(stored.DeprecationReason);

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => handler.Handle(new UpdatePermissionCommand(permission.Id,
            new UpdatePermissionRequest("Identity.B", "x", null, false, null)), default));
        Assert.Equal("duplicate_permission_key", ex.ErrorCode);
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new UpdatePermissionCommand(Guid.NewGuid(),
            new UpdatePermissionRequest("k", "d", null, false, null)), default));
    }

    [Fact]
    public async Task DeletePermission_is_a_soft_delete_with_a_default_or_given_reason()
    {
        var a = await Builders.AddPermissionAsync(_db.Uow, "Identity.A");
        var b = await Builders.AddPermissionAsync(_db.Uow, "Identity.B");
        var handler = new DeletePermissionCommandHandler(_db.Uow);

        await handler.Handle(new DeletePermissionCommand(a.Id, null), default);
        await handler.Handle(new DeletePermissionCommand(b.Id, "obsolete"), default);

        using var verify = _db.NewContext();
        var stored = await verify.Permissions.ToDictionaryAsync(p => p.Key);
        Assert.True(stored["Identity.A"].IsDeprecated);
        Assert.Equal("deleted_via_api", stored["Identity.A"].DeprecationReason);
        Assert.Equal("obsolete", stored["Identity.B"].DeprecationReason);
        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new DeletePermissionCommand(Guid.NewGuid(), null), default));
    }

    [Fact]
    public async Task GetPermissionById_and_Paged_work_and_404()
    {
        var p = await Builders.AddPermissionAsync(_db.Uow, "Identity.B");
        await Builders.AddPermissionAsync(_db.Uow, "Identity.A");

        var dto = await new GetPermissionByIdQueryHandler(_db.Uow).Handle(new GetPermissionByIdQuery(p.Id), default);
        var page = await new GetPermissionsPagedQueryHandler(_db.Uow).Handle(new GetPermissionsPagedQuery(1, 10), default);

        Assert.Equal("Identity.B", dto.Key);
        Assert.Equal(new[] { "Identity.A", "Identity.B" }, page.Items.Select(i => i.Key).ToArray());
        await Assert.ThrowsAsync<NotFoundException>(() => new GetPermissionByIdQueryHandler(_db.Uow).Handle(new GetPermissionByIdQuery(Guid.NewGuid()), default));
    }
}
