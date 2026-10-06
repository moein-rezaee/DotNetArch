namespace IdentityService.Domain.Interfaces;

public interface IOtpClient
{
    Task SendCodeAsync(string phoneNumber, CancellationToken cancellationToken = default);
    Task<bool> VerifyCodeAsync(string phoneNumber, string code, CancellationToken cancellationToken = default);
}

