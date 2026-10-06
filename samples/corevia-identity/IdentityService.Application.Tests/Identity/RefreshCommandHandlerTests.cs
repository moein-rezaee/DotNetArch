using System.IdentityModel.Tokens.Jwt;
using System.Net;
using Corevia.Kit.ErrorHandling.Abstractions.Exceptions;
using IdentityService.Application.Features.Identity.Commands.Refresh;
using IdentityService.Application.Features.Identity.Models;
using IdentityService.Application.Tests.Support;
using IdentityService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IdentityService.Application.Tests.Identity;

public sealed class RefreshCommandHandlerTests : IDisposable
{
    private readonly TestDb _db = TestDb.Create();

    public void Dispose() => _db.Dispose();

    private RefreshCommandHandler Handler()
        => new(_db.Uow, Builders.JwtService(), Options.Create(Builders.Jwt()));

    private static RefreshCommand Command(string token) => new(new RefreshRequest(token));

    private async Task<RefreshToken> AddTokenAsync(
        User user,
        string token,
        DateTime? expiresAt = null,
        DateTime? revokedAt = null,
        Guid? replacedBy = null,
        Guid? clientId = null,
        Guid? sessionId = null)
    {
        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = token,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            ExpiresAt = expiresAt,
            RevokedAt = revokedAt,
            ReplacedByTokenId = replacedBy,
            ClientId = clientId,
            UserSessionId = sessionId
        };
        await _db.Uow.Repository<RefreshToken>().AddAsync(entity);
        await _db.Uow.SaveChangesAsync();
        return entity;
    }

    [Fact]
    public async Task Rotation_revokes_old_token_links_ReplacedByTokenId_and_issues_a_non_expiring_replacement()
    {
        var user = await Builders.AddUserAsync(_db.Uow);
        var session = new UserSession { Id = Guid.NewGuid(), UserId = user.Id, CreatedAt = DateTime.UtcNow };
        await _db.Uow.Repository<UserSession>().AddAsync(session);
        var old = await AddTokenAsync(user, "old-token-value", sessionId: session.Id);

        var (returnedUser, access, refresh) = await Handler().Handle(Command("old-token-value"), default);

        Assert.Equal(user.Id, returnedUser.Id);
        Assert.NotEqual("old-token-value", refresh);

        using var verify = _db.NewContext();
        var tokens = await verify.RefreshTokens.ToListAsync();
        var oldStored = tokens.Single(t => t.Id == old.Id);
        var fresh = tokens.Single(t => t.Token == refresh);
        Assert.NotNull(oldStored.RevokedAt);
        Assert.Equal(fresh.Id, oldStored.ReplacedByTokenId);
        Assert.Null(fresh.ExpiresAt);
        Assert.Null(fresh.RevokedAt);
        Assert.Equal(session.Id, fresh.UserSessionId);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(access);
        Assert.Equal(fresh.JwtId, jwt.Id);
        Assert.Equal(session.Id.ToString(), jwt.Claims.Single(c => c.Type == "sid").Value);
    }

    [Fact]
    public async Task Customer_refresh_token_never_expires_across_multiple_rotations()
    {
        var user = await Builders.AddUserAsync(_db.Uow);
        await AddTokenAsync(user, "t0-token-value");
        var handler = Handler();

        var r1 = await handler.Handle(Command("t0-token-value"), default);
        var r2 = await handler.Handle(Command(r1.RefreshToken), default);
        var r3 = await handler.Handle(Command(r2.RefreshToken), default);

        using var verify = _db.NewContext();
        var live = await verify.RefreshTokens.SingleAsync(t => t.Token == r3.RefreshToken);
        Assert.Null(live.ExpiresAt);
        Assert.Equal(1, await verify.RefreshTokens.CountAsync(t => t.RevokedAt == null));
        Assert.Equal(3, await verify.RefreshTokens.CountAsync(t => t.RevokedAt != null));
    }

    [Fact]
    public async Task Legacy_token_with_a_future_expiry_is_still_honoured_and_rotates_to_a_non_expiring_one()
    {
        var user = await Builders.AddUserAsync(_db.Uow);
        await AddTokenAsync(user, "legacy-token-value", expiresAt: DateTime.UtcNow.AddDays(3));

        var (_, _, refresh) = await Handler().Handle(Command("legacy-token-value"), default);

        Assert.Null((await _db.NewContext().RefreshTokens.SingleAsync(t => t.Token == refresh)).ExpiresAt);
    }

    [Fact]
    public async Task Legacy_token_past_its_expiry_is_unauthorized_invalid_refresh_token()
    {
        var user = await Builders.AddUserAsync(_db.Uow);
        await AddTokenAsync(user, "expired-token-value", expiresAt: DateTime.UtcNow.AddMinutes(-1));

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => Handler().Handle(Command("expired-token-value"), default));

        Assert.Equal("invalid_refresh_token", ex.ErrorCode);
        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
    }

    [Fact]
    public async Task Unknown_token_is_unauthorized_invalid_refresh_token()
    {
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => Handler().Handle(Command("does-not-exist"), default));

        Assert.Equal("invalid_refresh_token", ex.ErrorCode);
    }

    [Fact]
    public async Task Revoked_token_outside_the_grace_window_is_rejected_even_when_it_has_a_replacement()
    {
        var user = await Builders.AddUserAsync(_db.Uow);
        var replacement = await AddTokenAsync(user, "replacement-token");
        await AddTokenAsync(user, "stale-token-value", revokedAt: DateTime.UtcNow.AddSeconds(-60), replacedBy: replacement.Id);

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => Handler().Handle(Command("stale-token-value"), default));

        Assert.Equal("invalid_refresh_token", ex.ErrorCode);
    }

    [Fact]
    public async Task Revoked_token_within_the_grace_window_replays_the_replacement_without_rotating_again()
    {
        var user = await Builders.AddUserAsync(_db.Uow);
        var replacement = await AddTokenAsync(user, "replacement-token");
        await AddTokenAsync(user, "just-rotated-token", revokedAt: DateTime.UtcNow.AddSeconds(-2), replacedBy: replacement.Id);

        var (_, access, refresh) = await Handler().Handle(Command("just-rotated-token"), default);

        Assert.Equal("replacement-token", refresh);
        Assert.False(string.IsNullOrWhiteSpace(access));
        using var verify = _db.NewContext();
        Assert.Equal(2, await verify.RefreshTokens.CountAsync()); // no new token minted
        var stored = await verify.RefreshTokens.SingleAsync(t => t.Token == "replacement-token");
        Assert.NotNull(stored.JwtId); // access token jti recorded on the replacement
    }

    [Fact]
    public async Task Revoked_token_within_the_grace_window_whose_replacement_is_also_revoked_is_rejected()
    {
        var user = await Builders.AddUserAsync(_db.Uow);
        var replacement = await AddTokenAsync(user, "replacement-token", revokedAt: DateTime.UtcNow.AddSeconds(-1));
        await AddTokenAsync(user, "just-rotated-token", revokedAt: DateTime.UtcNow.AddSeconds(-2), replacedBy: replacement.Id);

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => Handler().Handle(Command("just-rotated-token"), default));

        Assert.Equal("invalid_refresh_token", ex.ErrorCode);
    }

    [Fact]
    public async Task Logged_out_token_without_a_replacement_is_rejected_even_inside_the_grace_window()
    {
        var user = await Builders.AddUserAsync(_db.Uow);
        await AddTokenAsync(user, "logged-out-token", revokedAt: DateTime.UtcNow.AddSeconds(-1));

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => Handler().Handle(Command("logged-out-token"), default));

        Assert.Equal("invalid_refresh_token", ex.ErrorCode);
    }

    [Fact]
    public async Task Token_of_a_missing_user_is_not_found()
    {
        var ghost = new User { Id = Guid.NewGuid(), PhoneNumber = "09000000000", CreatedAt = DateTime.UtcNow };
        // Disable FK enforcement scope: SQLite connection has FKs off by default and the model declares no FK to Users.
        await AddTokenAsync(ghost, "orphan-token-value");

        await Assert.ThrowsAsync<NotFoundException>(() => Handler().Handle(Command("orphan-token-value"), default));
    }

    [Fact]
    public async Task Client_bound_token_gets_the_client_scopes_and_keeps_the_client()
    {
        var user = await Builders.AddUserAsync(_db.Uow);
        var client = await Builders.AddClientAsync(_db.Uow, "web-public");
        var scope = await Builders.AddScopeAsync(_db.Uow, "catalog.read");
        await Builders.AddScopeAsync(_db.Uow, "other");
        await Builders.LinkClientScopeAsync(_db.Uow, client.Id, scope.Id);
        await AddTokenAsync(user, "client-token-value", clientId: client.Id);

        var (_, access, refresh) = await Handler().Handle(Command("client-token-value"), default);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(access);
        Assert.Equal(new[] { "catalog.read" }, jwt.Claims.Where(c => c.Type == "scope").Select(c => c.Value).ToArray());
        Assert.Equal(client.Id, (await _db.NewContext().RefreshTokens.SingleAsync(t => t.Token == refresh)).ClientId);
    }

    [Fact]
    public async Task SuperAdmin_token_gets_every_scope()
    {
        var user = await Builders.AddUserAsync(_db.Uow);
        var superAdmin = await Builders.AddRoleAsync(_db.Uow, "SuperAdmin");
        await Builders.LinkUserRoleAsync(_db.Uow, user.Id, superAdmin.Id);
        await Builders.AddScopeAsync(_db.Uow, "a.read");
        await Builders.AddScopeAsync(_db.Uow, "b.write");
        await AddTokenAsync(user, "admin-token-value");

        var (_, access, _) = await Handler().Handle(Command("admin-token-value"), default);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(access);
        Assert.Equal(new[] { "a.read", "b.write" }, jwt.Claims.Where(c => c.Type == "scope").Select(c => c.Value).OrderBy(x => x).ToArray());
        Assert.Contains(jwt.Claims, c => c is { Type: "role", Value: "SuperAdmin" });
    }
}
