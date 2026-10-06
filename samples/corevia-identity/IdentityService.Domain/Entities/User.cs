namespace IdentityService.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string PhoneNumber { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsActive { get; set; } = true;
}
