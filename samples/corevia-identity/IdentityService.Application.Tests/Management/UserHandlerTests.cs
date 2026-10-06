namespace IdentityService.Application.Tests.Management;

public sealed class UserHandlerTests : IDisposable
{
    private readonly TestDb _db = TestDb.Create();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task CreateUser_trims_the_phone_and_persists_an_active_user()
    {
        var dto = await new CreateUserCommandHandler(_db.Uow)
            .Handle(new CreateUserCommand(new CreateUserRequest("  09121112222  ", true)), default);

        Assert.Equal("09121112222", dto.PhoneNumber);
        Assert.True(dto.IsActive);
        Assert.Empty(dto.Roles);
        Assert.Empty(dto.Tenants);
        Assert.Equal("09121112222", (await _db.NewContext().Users.SingleAsync()).PhoneNumber);
    }

    [Fact]
    public async Task CreateUser_duplicate_phone_is_bad_request_duplicate_phone_number()
    {
        await Builders.AddUserAsync(_db.Uow, "09121112222");

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => new CreateUserCommandHandler(_db.Uow)
            .Handle(new CreateUserCommand(new CreateUserRequest("09121112222", true)), default));

        Assert.Equal("duplicate_phone_number", ex.ErrorCode);
        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
    }

    [Fact]
    public async Task UpdateUser_changes_phone_and_activation_and_returns_roles_and_tenants()
    {
        var user = await Builders.AddUserAsync(_db.Uow, "09120000001");
        var role = await Builders.AddRoleAsync(_db.Uow, "Customer");
        var tenant = await Builders.AddTenantAsync(_db.Uow, "acme");
        await Builders.LinkUserRoleAsync(_db.Uow, user.Id, role.Id);
        await Builders.LinkUserTenantAsync(_db.Uow, user.Id, tenant.Id);

        var dto = await new UpdateUserCommandHandler(_db.Uow)
            .Handle(new UpdateUserCommand(user.Id, new UpdateUserRequest("09120000002", false)), default);

        Assert.Equal("09120000002", dto.PhoneNumber);
        Assert.False(dto.IsActive);
        Assert.NotNull(dto.UpdatedAt);
        Assert.Equal("Customer", Assert.Single(dto.Roles).Name);
        Assert.Equal("acme", Assert.Single(dto.Tenants).Name);
    }

    [Fact]
    public async Task UpdateUser_keeping_the_same_phone_is_not_a_duplicate()
    {
        var user = await Builders.AddUserAsync(_db.Uow, "09120000001");

        var dto = await new UpdateUserCommandHandler(_db.Uow)
            .Handle(new UpdateUserCommand(user.Id, new UpdateUserRequest("09120000001", true)), default);

        Assert.Equal("09120000001", dto.PhoneNumber);
    }

    [Fact]
    public async Task UpdateUser_to_another_users_phone_is_bad_request()
    {
        await Builders.AddUserAsync(_db.Uow, "09120000001");
        var other = await Builders.AddUserAsync(_db.Uow, "09120000002");

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => new UpdateUserCommandHandler(_db.Uow)
            .Handle(new UpdateUserCommand(other.Id, new UpdateUserRequest("09120000001", true)), default));

        Assert.Equal("duplicate_phone_number", ex.ErrorCode);
    }

    [Fact]
    public async Task UpdateUser_unknown_id_is_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => new UpdateUserCommandHandler(_db.Uow)
            .Handle(new UpdateUserCommand(Guid.NewGuid(), new UpdateUserRequest("09120000009", true)), default));
    }

    [Fact]
    public async Task DeleteUser_soft_deletes_by_deactivating_and_keeps_the_row()
    {
        var user = await Builders.AddUserAsync(_db.Uow);

        await new DeleteUserCommandHandler(_db.Uow).Handle(new DeleteUserCommand(user.Id), default);

        var stored = await _db.NewContext().Users.SingleAsync();
        Assert.False(stored.IsActive);
        Assert.NotNull(stored.UpdatedAt);
    }

    [Fact]
    public async Task DeleteUser_unknown_id_is_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => new DeleteUserCommandHandler(_db.Uow)
            .Handle(new DeleteUserCommand(Guid.NewGuid()), default));
    }

    [Fact]
    public async Task GetUserById_returns_detail_with_roles_and_tenants_and_404s_when_missing()
    {
        var user = await Builders.AddUserAsync(_db.Uow);
        var role = await Builders.AddRoleAsync(_db.Uow, "Customer");
        await Builders.LinkUserRoleAsync(_db.Uow, user.Id, role.Id);

        var dto = await new GetUserByIdQueryHandler(_db.Uow).Handle(new GetUserByIdQuery(user.Id), default);

        Assert.Equal(user.PhoneNumber, dto.PhoneNumber);
        Assert.Equal("Customer", Assert.Single(dto.Roles).Name);
        await Assert.ThrowsAsync<NotFoundException>(() => new GetUserByIdQueryHandler(_db.Uow).Handle(new GetUserByIdQuery(Guid.NewGuid()), default));
    }

    [Fact]
    public async Task GetUsersPaged_pages_only_active_users_in_creation_order()
    {
        for (var i = 0; i < 5; i++)
        {
            await Builders.AddUserAsync(_db.Uow, $"0912000000{i}");
        }

        await Builders.AddUserAsync(_db.Uow, "09129999999", active: false);

        var page = await new GetUsersPagedQueryHandler(_db.Uow)
            .Handle(new GetUsersPagedQuery(2, 2, null, null, null), default);

        Assert.Equal(5, page.TotalCount);
        Assert.Equal(2, page.PageNumber);
        Assert.Equal(2, page.Items.Count);
    }

    [Fact]
    public async Task GetUsersPaged_filters_by_tenant_and_role()
    {
        var a = await Builders.AddUserAsync(_db.Uow, "09120000001");
        var b = await Builders.AddUserAsync(_db.Uow, "09120000002");
        var tenant = await Builders.AddTenantAsync(_db.Uow, "acme");
        var role = await Builders.AddRoleAsync(_db.Uow, "Admin");
        await Builders.LinkUserTenantAsync(_db.Uow, a.Id, tenant.Id);
        await Builders.LinkUserRoleAsync(_db.Uow, b.Id, role.Id);

        var handler = new GetUsersPagedQueryHandler(_db.Uow);
        var byTenant = await handler.Handle(new GetUsersPagedQuery(1, 10, null, tenant.Id, null), default);
        var byRole = await handler.Handle(new GetUsersPagedQuery(1, 10, null, null, role.Id), default);

        Assert.Equal(a.Id, Assert.Single(byTenant.Items).Id);
        Assert.Equal(b.Id, Assert.Single(byRole.Items).Id);
    }

    [Fact]
    public async Task GetUsersPaged_filters_by_phone_fragment()
    {
        await Builders.AddUserAsync(_db.Uow, "09121110000");
        await Builders.AddUserAsync(_db.Uow, "09352220000");

        var page = await new GetUsersPagedQueryHandler(_db.Uow)
            .Handle(new GetUsersPagedQuery(1, 10, "0935", null, null), default);

        Assert.Equal("09352220000", Assert.Single(page.Items).PhoneNumber);
    }
}
