using IdentityService.Application.Features.Identity.Options;
using IdentityService.Application.Features.Identity.Services;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using Microsoft.Extensions.Options;

namespace IdentityService.Application.Tests.Support;

public static class Builders
{
    public const string JwtSecret = "unit-test-signing-key-which-is-at-least-32-bytes-long";

    public static JwtOptions Jwt() => new()
    {
        Issuer = "IdentityService.Tests",
        Audience = "IdentityClients.Tests",
        Secret = JwtSecret,
        AccessTokenMinutes = 15,
        RefreshTokenDays = 30
    };

    public static IJwtService JwtService() => new JwtService(Options.Create(Jwt()));

    public static async Task<User> AddUserAsync(IUnitOfWork uow, string phone = "09120000001", bool active = true)
    {
        var user = new User { Id = Guid.NewGuid(), PhoneNumber = phone, CreatedAt = DateTime.UtcNow, IsActive = active };
        await uow.Repository<User>().AddAsync(user);
        await uow.SaveChangesAsync();
        return user;
    }

    public static async Task<Role> AddRoleAsync(IUnitOfWork uow, string name)
    {
        var role = new Role { Id = Guid.NewGuid(), Name = name, DisplayName = name, CreatedAt = DateTime.UtcNow };
        await uow.Repository<Role>().AddAsync(role);
        await uow.SaveChangesAsync();
        return role;
    }

    public static async Task<Scope> AddScopeAsync(IUnitOfWork uow, string name)
    {
        var scope = new Scope { Id = Guid.NewGuid(), Name = name, DisplayName = name, CreatedAt = DateTime.UtcNow };
        await uow.Repository<Scope>().AddAsync(scope);
        await uow.SaveChangesAsync();
        return scope;
    }

    public static async Task<Permission> AddPermissionAsync(IUnitOfWork uow, string key)
    {
        var permission = new Permission { Id = Guid.NewGuid(), Key = key, DisplayName = key, CreatedAt = DateTime.UtcNow };
        await uow.Repository<Permission>().AddAsync(permission);
        await uow.SaveChangesAsync();
        return permission;
    }

    public static async Task<Client> AddClientAsync(IUnitOfWork uow, string publicId, bool active = true)
    {
        var client = new Client { Id = Guid.NewGuid(), ClientId = publicId, Name = publicId, IsActive = active, CreatedAt = DateTime.UtcNow };
        await uow.Repository<Client>().AddAsync(client);
        await uow.SaveChangesAsync();
        return client;
    }

    public static async Task<Tenant> AddTenantAsync(IUnitOfWork uow, string name, bool active = true)
    {
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = name, DisplayName = name, IsActive = active, CreatedAt = DateTime.UtcNow };
        await uow.Repository<Tenant>().AddAsync(tenant);
        await uow.SaveChangesAsync();
        return tenant;
    }

    public static async Task LinkClientScopeAsync(IUnitOfWork uow, Guid clientId, Guid scopeId)
    {
        await uow.Repository<ClientScope>().AddAsync(new ClientScope { ClientId = clientId, ScopeId = scopeId });
        await uow.SaveChangesAsync();
    }

    public static async Task LinkUserRoleAsync(IUnitOfWork uow, Guid userId, Guid roleId)
    {
        await uow.Repository<UserRole>().AddAsync(new UserRole { UserId = userId, RoleId = roleId });
        await uow.SaveChangesAsync();
    }

    public static async Task LinkUserTenantAsync(IUnitOfWork uow, Guid userId, Guid tenantId, bool isDefault = false)
    {
        await uow.Repository<UserTenant>().AddAsync(new UserTenant { UserId = userId, TenantId = tenantId, IsDefault = isDefault });
        await uow.SaveChangesAsync();
    }
}
