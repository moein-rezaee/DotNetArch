using Corevia.Kit.ErrorHandling.Abstractions.Exceptions;
using IdentityService.Api.Tests.Support;
using IdentityService.Application.Features.Identity.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Api.Tests;

public sealed class IdentityControllerTests : IDisposable
{
    private readonly ApiFixture _api = new();

    public void Dispose() => _api.Dispose();

    [Fact]
    public async Task Send_returns_202_with_accepted_true_and_delivery_queued()
    {
        var result = await _api.Identity().Send(new SendCodeRequest("09121234567"), default);

        var accepted = Assert.IsType<AcceptedResult>(result);
        var body = Assert.IsType<SendCodeResponse>(accepted.Value);
        Assert.True(body.Accepted);
        Assert.Equal("queued", body.Delivery);
        Assert.Equal(StatusCodes.Status202Accepted, accepted.StatusCode);
        Assert.Equal(new[] { "09121234567" }, _api.Otp.Sent);
    }

    [Fact]
    public async Task Verify_returns_access_and_refresh_tokens_and_records_remote_ip_and_user_agent()
    {
        var controller = _api.Identity();
        controller.HttpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.9");
        controller.HttpContext.Request.Headers.UserAgent = "xunit/1.0";

        var result = await controller.Verify(new VerifyCodeRequest("09121234567", "12345"), default);

        var tokens = Assert.IsType<TokenResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.False(string.IsNullOrWhiteSpace(tokens.AccessToken));
        var session = await _api.Db.UserSessions.SingleAsync();
        Assert.Equal("203.0.113.9", session.IpAddress);
        Assert.Equal("xunit/1.0", session.DeviceInfo);
    }

    [Fact]
    public async Task Verify_with_a_wrong_code_surfaces_UnauthorizedException()
    {
        _api.Otp.VerifyResult = false;

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _api.Identity().Verify(new VerifyCodeRequest("09121234567", "00000"), default));

        Assert.Equal("invalid_verification_code", ex.ErrorCode);
    }

    [Fact]
    public async Task Refresh_rotates_and_Logout_revokes_through_the_controller()
    {
        var controller = _api.Identity();
        var login = Assert.IsType<TokenResponse>(Assert.IsType<OkObjectResult>(
            (await controller.Verify(new VerifyCodeRequest("09121234567", "12345"), default)).Result).Value);

        var refreshed = Assert.IsType<TokenResponse>(Assert.IsType<OkObjectResult>(
            (await controller.Refresh(new RefreshRequest(login.RefreshToken), default)).Result).Value);
        Assert.NotEqual(login.RefreshToken, refreshed.RefreshToken);

        Assert.IsType<OkResult>(await controller.Logout(new LogoutRequest(refreshed.RefreshToken), default));
        Assert.Null((await _api.Db.RefreshTokens.AsNoTracking().ToListAsync()).SingleOrDefault(t => t.Token == refreshed.RefreshToken && t.RevokedAt is null));
    }

    [Fact]
    public async Task Refresh_with_an_unknown_token_surfaces_UnauthorizedException()
    {
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _api.Identity().Refresh(new RefreshRequest(new string('x', 40)), default));

        Assert.Equal("invalid_refresh_token", ex.ErrorCode);
    }
}
