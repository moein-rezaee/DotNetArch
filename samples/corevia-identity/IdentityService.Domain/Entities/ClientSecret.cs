namespace IdentityService.Domain.Entities;

public class ClientSecret
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public string Hash { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}

