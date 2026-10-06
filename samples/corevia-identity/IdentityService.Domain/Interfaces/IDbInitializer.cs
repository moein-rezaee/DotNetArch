namespace IdentityService.Domain.Interfaces;

public interface IDbInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
