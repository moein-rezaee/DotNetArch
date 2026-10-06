namespace IdentityService.Domain.Entities;

public class UserTenant
{
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public bool IsDefault { get; set; }
}

