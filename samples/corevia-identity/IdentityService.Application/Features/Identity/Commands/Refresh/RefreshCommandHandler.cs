using Corevia.Kit.ErrorHandling.Abstractions.Exceptions;
using IdentityService.Application.Features.Identity.Options;
using IdentityService.Application.Features.Identity.Services;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Options;

namespace IdentityService.Application.Features.Identity.Commands.Refresh;

public class RefreshCommandHandler(IUnitOfWork uow, IJwtService jwt, IOptions<JwtOptions> jwtOptions)
    : IRequestHandler<RefreshCommand, (User User, string AccessToken, string RefreshToken)>
{
    // Two overlapping refresh calls for the same client (page-load double-fire, multiple tabs,
    // a retry after a slow request) commonly present the SAME refresh token concurrently. Without
    // this window, the request that loses the race hits an already-rotated token and gets a hard
    // 401 that the frontend reads as "logged out" even though the session is still valid.
    private static readonly TimeSpan RotationGraceWindow = TimeSpan.FromSeconds(10);

    private readonly IUnitOfWork _uow = uow;
    private readonly IJwtService _jwt = jwt;
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    public async Task<(User User, string AccessToken, string RefreshToken)> Handle(RefreshCommand request, CancellationToken cancellationToken)
    {
        var rtRepo = _uow.Repository<RefreshToken>();
        var token = await rtRepo.FirstOrDefaultAsync(x => x.Token == request.Request.RefreshToken, cancellationToken);

        if (token is not null && token.RevokedAt is not null)
        {
            var withinGraceWindow = DateTime.UtcNow - token.RevokedAt.Value <= RotationGraceWindow;
            if (withinGraceWindow && token.ReplacedByTokenId.HasValue)
            {
                var replacement = await rtRepo.FirstOrDefaultAsync(
                    x => x.Id == token.ReplacedByTokenId.Value && x.RevokedAt == null, cancellationToken);
                if (replacement is not null)
                {
                    return await IssueForExistingTokenAsync(replacement, cancellationToken);
                }
            }

            throw new UnauthorizedException("Refresh token is invalid or has expired.", "invalid_refresh_token");
        }

        if (token is null || (token.ExpiresAt.HasValue && token.ExpiresAt.Value <= DateTime.UtcNow))
        {
            throw new UnauthorizedException("Refresh token is invalid or has expired.", "invalid_refresh_token");
        }

        var user = await _uow.Repository<User>().GetByIdAsync(token.UserId, cancellationToken)
                   ?? throw new NotFoundException("User not found.");

        // rotate token
        token.RevokedAt = DateTime.UtcNow;

        Guid? clientId = token.ClientId;

        var newRefresh = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = _jwt.GenerateRefreshToken(),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = null,
            ClientId = clientId,
            UserSessionId = token.UserSessionId
        };
        await rtRepo.AddAsync(newRefresh, cancellationToken);
        token.ReplacedByTokenId = newRefresh.Id;
        await _uow.SaveChangesAsync(cancellationToken);

        return await IssueAccessTokenAsync(user, newRefresh, clientId, cancellationToken);
    }

    private async Task<(User User, string AccessToken, string RefreshToken)> IssueForExistingTokenAsync(
        RefreshToken replacement, CancellationToken cancellationToken)
    {
        var user = await _uow.Repository<User>().GetByIdAsync(replacement.UserId, cancellationToken)
                   ?? throw new NotFoundException("User not found.");

        return await IssueAccessTokenAsync(user, replacement, replacement.ClientId, cancellationToken);
    }

    private async Task<(User User, string AccessToken, string RefreshToken)> IssueAccessTokenAsync(
        User user, RefreshToken refresh, Guid? clientId, CancellationToken cancellationToken)
    {
        var userRoleRepo = _uow.Repository<UserRole>();
        var roleRepo = _uow.Repository<Role>();
        var roleNames = (from ur in userRoleRepo.Query()
                         join r in roleRepo.Query() on ur.RoleId equals r.Id
                         where ur.UserId == user.Id
                         select r.Name).ToList();
        var isSuperAdmin = roleNames.Any(r => string.Equals(r, "SuperAdmin", StringComparison.Ordinal));

        List<string>? scopeNames = null;
        if (isSuperAdmin)
        {
            var scopeRepo = _uow.Repository<Scope>();
            scopeNames = scopeRepo.Query()
                .Select(s => s.Name)
                .Distinct()
                .ToList();
        }
        else if (clientId.HasValue)
        {
            var clientScopeRepo = _uow.Repository<ClientScope>();
            var scopeRepo = _uow.Repository<Scope>();

            var scopeIds = clientScopeRepo.Query()
                .Where(cs => cs.ClientId == clientId.Value)
                .Select(cs => cs.ScopeId)
                .ToList();

            if (scopeIds.Count > 0)
            {
                scopeNames = scopeRepo.Query()
                    .Where(s => scopeIds.Contains(s.Id))
                    .Select(s => s.Name)
                    .Distinct()
                    .ToList();
            }
        }

        var tenantId = ResolveTokenTenantId(user.Id);
        var access = _jwt.GenerateAccessToken(user, refresh.UserSessionId, roleNames, clientId, scopeNames, out var jti, tenantId);
        refresh.JwtId = jti;
        await _uow.SaveChangesAsync(cancellationToken);
        return (user, access, refresh.Token);
    }

    private Guid? ResolveTokenTenantId(Guid userId)
    {
        var mappings = _uow.Repository<UserTenant>().Query()
            .Where(mapping => mapping.UserId == userId)
            .ToList();

        if (mappings.Count == 1)
        {
            return mappings[0].TenantId;
        }

        var defaults = mappings.Where(mapping => mapping.IsDefault).ToList();
        return defaults.Count == 1 ? defaults[0].TenantId : null;
    }
}
