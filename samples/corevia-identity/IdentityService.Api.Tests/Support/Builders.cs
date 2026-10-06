using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;

namespace IdentityService.Api.Tests.Support;

public static class Builders
{
    public static async Task<User> AddUserAsync(IUnitOfWork uow, string phone = "09120000001")
    {
        var user = new User { Id = Guid.NewGuid(), PhoneNumber = phone, CreatedAt = DateTime.UtcNow };
        await uow.Repository<User>().AddAsync(user);
        await uow.SaveChangesAsync();
        return user;
    }

    public static async Task<Client> AddClientAsync(IUnitOfWork uow, string publicId, bool active = true)
    {
        var client = new Client { Id = Guid.NewGuid(), ClientId = publicId, Name = publicId, IsActive = active, CreatedAt = DateTime.UtcNow };
        await uow.Repository<Client>().AddAsync(client);
        await uow.SaveChangesAsync();
        return client;
    }

    public static async Task<Scope> AddScopeAsync(IUnitOfWork uow, string name)
    {
        var scope = new Scope { Id = Guid.NewGuid(), Name = name, DisplayName = name, CreatedAt = DateTime.UtcNow };
        await uow.Repository<Scope>().AddAsync(scope);
        await uow.SaveChangesAsync();
        return scope;
    }

    public static async Task LinkClientScopeAsync(IUnitOfWork uow, Guid clientId, Guid scopeId)
    {
        await uow.Repository<ClientScope>().AddAsync(new ClientScope { ClientId = clientId, ScopeId = scopeId });
        await uow.SaveChangesAsync();
    }
}
