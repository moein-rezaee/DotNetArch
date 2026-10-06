using System.Net;
using Corevia.Kit.ErrorHandling.Abstractions.Exceptions;
using IdentityService.Application.Features.Identity.Commands.Logout;
using IdentityService.Application.Features.Identity.Commands.Send;
using IdentityService.Application.Features.Identity.Models;
using IdentityService.Application.Tests.Support;
using IdentityService.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Application.Tests.Identity;

public sealed class LogoutAndSendTests : IDisposable
{
    private readonly TestDb _db = TestDb.Create();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Logout_revokes_the_refresh_token_and_ends_its_session()
    {
        var logoutToken = Guid.NewGuid().ToString("N");
        var user = await Builders.AddUserAsync(_db.Uow);
        var session = new UserSession { Id = Guid.NewGuid(), UserId = user.Id, CreatedAt = DateTime.UtcNow };
        await _db.Uow.Repository<UserSession>().AddAsync(session);
        await _db.Uow.Repository<RefreshToken>().AddAsync(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = logoutToken,
            CreatedAt = DateTime.UtcNow,
            UserSessionId = session.Id
        });
        await _db.Uow.SaveChangesAsync();

        await new LogoutCommandHandler(_db.Uow).Handle(new LogoutCommand(new LogoutRequest(logoutToken)), default);

        using var verify = _db.NewContext();
        Assert.NotNull((await verify.RefreshTokens.SingleAsync()).RevokedAt);
        var storedSession = await verify.UserSessions.SingleAsync();
        Assert.True(storedSession.IsRevoked);
        Assert.NotNull(storedSession.EndedAt);
    }

    [Fact]
    public async Task Logout_with_an_unknown_token_is_a_silent_no_op()
    {
        await new LogoutCommandHandler(_db.Uow).Handle(new LogoutCommand(new LogoutRequest("nope")), default);

        Assert.Empty(await _db.NewContext().RefreshTokens.ToListAsync());
    }

    [Fact]
    public async Task Logout_does_not_touch_an_already_revoked_token()
    {
        var revokedToken = Guid.NewGuid().ToString("N");
        var user = await Builders.AddUserAsync(_db.Uow);
        var revokedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await _db.Uow.Repository<RefreshToken>().AddAsync(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = revokedToken,
            CreatedAt = DateTime.UtcNow,
            RevokedAt = revokedAt
        });
        await _db.Uow.SaveChangesAsync();

        await new LogoutCommandHandler(_db.Uow).Handle(new LogoutCommand(new LogoutRequest(revokedToken)), default);

        Assert.Equal(revokedAt, (await _db.NewContext().RefreshTokens.SingleAsync()).RevokedAt);
    }

    [Fact]
    public async Task Send_delegates_the_phone_number_to_the_otp_port()
    {
        var otp = new FakeOtpClient();

        await new SendCommandHandler(otp).Handle(new SendCommand(new SendCodeRequest("09121234567")), default);

        Assert.Equal(new[] { "09121234567" }, otp.SentTo);
    }

    [Fact]
    public async Task Send_propagates_a_bad_request_from_the_otp_port()
    {
        var otp = new FakeOtpClient { ThrowOnSend = new BadRequestException("blocked", "otp_send_bad_request") };

        var ex = await Assert.ThrowsAsync<BadRequestException>(() =>
            new SendCommandHandler(otp).Handle(new SendCommand(new SendCodeRequest("09121234567")), default));

        Assert.Equal("otp_send_bad_request", ex.ErrorCode);
    }

    [Fact]
    public async Task Send_propagates_an_external_service_failure_from_the_otp_port()
    {
        var otp = new FakeOtpClient { ThrowOnSend = new ExternalServiceException("down", HttpStatusCode.GatewayTimeout) };

        var ex = await Assert.ThrowsAsync<ExternalServiceException>(() =>
            new SendCommandHandler(otp).Handle(new SendCommand(new SendCodeRequest("09121234567")), default));

        Assert.Equal(HttpStatusCode.GatewayTimeout, ex.StatusCode);
    }
}
