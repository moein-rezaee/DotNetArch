namespace IdentityService.Domain.Entities;

public class Client
{
    public Guid Id { get; set; }
    public string ClientId { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

