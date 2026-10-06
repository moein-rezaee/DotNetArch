namespace IdentityService.Domain.Entities;

public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Token { get; set; } = default!;
    // A customer session remains valid until its token is revoked by logout.
    // Existing tokens may still carry an expiry; new OTP sessions use null.
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public Guid? ClientId { get; set; }
    public string? JwtId { get; set; }
    public string? RevokedReason { get; set; }
    public Guid? UserSessionId { get; set; }
    // Set when this token is rotated during a Refresh call. Lets a concurrent, overlapping
    // refresh request that presents this now-revoked token within the rotation grace window
    // be replayed against the resulting token instead of being hard-rejected as invalid.
    public Guid? ReplacedByTokenId { get; set; }
}
