using IdentityService.Domain.Interfaces;

namespace IdentityService.Application.Tests.Support;

public sealed class FakeOtpClient : IOtpClient
{
    public bool VerifyResult { get; set; } = true;

    public Exception? ThrowOnSend { get; set; }

    public Exception? ThrowOnVerify { get; set; }

    public List<string> SentTo { get; } = new();

    public List<(string Phone, string Code)> Verified { get; } = new();

    public Task SendCodeAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        if (ThrowOnSend is not null)
        {
            throw ThrowOnSend;
        }

        SentTo.Add(phoneNumber);
        return Task.CompletedTask;
    }

    public Task<bool> VerifyCodeAsync(string phoneNumber, string code, CancellationToken cancellationToken = default)
    {
        if (ThrowOnVerify is not null)
        {
            throw ThrowOnVerify;
        }

        Verified.Add((phoneNumber, code));
        return Task.FromResult(VerifyResult);
    }
}
