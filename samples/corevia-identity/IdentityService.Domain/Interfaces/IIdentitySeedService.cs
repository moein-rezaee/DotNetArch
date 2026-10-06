namespace IdentityService.Domain.Interfaces;

public interface IIdentitySeedService
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}
