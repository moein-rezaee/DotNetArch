namespace IdentityService.Domain.Entities;

public class Permission
{
    public Guid Id { get; set; }
    public string Key { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeprecated { get; set; }
    public string? DeprecationReason { get; set; }
}
