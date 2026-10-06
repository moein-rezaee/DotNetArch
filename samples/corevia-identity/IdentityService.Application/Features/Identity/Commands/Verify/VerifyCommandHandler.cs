using Corevia.Kit.ErrorHandling.Abstractions.Exceptions;
using IdentityService.Application.Features.Identity.Options;
using IdentityService.Application.Features.Identity.Services;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Options;

namespace IdentityService.Application.Features.Identity.Commands.Verify;

public class VerifyCommandHandler(
    IOtpClient otpClient,
    IUnitOfWork uow,
    IJwtService jwt,
    IOptions<JwtOptions> jwtOptions,
    IOptions<RootAdminOptions> rootAdminOptions,
    IOptions<IdentityClientOptions> identityClientOptions) : IRequestHandler<VerifyCommand, (User User, string AccessToken, string RefreshToken)>
{
    private readonly IOtpClient _otpClient = otpClient;
    private readonly IUnitOfWork _uow = uow;
    private readonly IJwtService _jwt = jwt;
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;
    private readonly RootAdminOptions _rootAdmin = rootAdminOptions.Value;
    private readonly IdentityClientOptions _identityClients = identityClientOptions.Value;

    public async Task<(User User, string AccessToken, string RefreshToken)> Handle(VerifyCommand request, CancellationToken cancellationToken)
    {
        var ok = await _otpClient.VerifyCodeAsync(request.Request.PhoneNumber, request.Request.Code, cancellationToken);
        if (!ok)
        {
            throw new UnauthorizedException("Verification code is invalid.", "invalid_verification_code");
        }

        var userRepo = _uow.Repository<User>();
        var user = await userRepo.FirstOrDefaultAsync(x => x.PhoneNumber == request.Request.PhoneNumber, cancellationToken);
        if (user is null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                PhoneNumber = request.Request.PhoneNumber,
                CreatedAt = DateTime.UtcNow
            };
            await userRepo.AddAsync(user, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);
        }

        await EnsureRootAdminAsync(user, cancellationToken);

        var userRoleRepo = _uow.Repository<UserRole>();
        var roleRepo = _uow.Repository<Role>();
        var roleNames = (from ur in userRoleRepo.Query()
                         join r in roleRepo.Query() on ur.RoleId equals r.Id
                         where ur.UserId == user.Id
                         select r.Name).ToList();
        var isSuperAdmin = roleNames.Any(r => string.Equals(r, "SuperAdmin", StringComparison.Ordinal));

        Guid? clientId = null;
        List<string>? scopeNames = null;
        var defaultClientPublicId = _identityClients.DefaultPublicClientId?.Trim();
        if (!string.IsNullOrWhiteSpace(defaultClientPublicId))
        {
            var clientRepo = _uow.Repository<Client>();
            var client = clientRepo.Query()
                .FirstOrDefault(c => c.ClientId == defaultClientPublicId && c.IsActive);

            if (client is null)
            {
                throw new UnauthorizedException("Client is invalid or inactive.", "invalid_client");
            }

            clientId = client.Id;

            var clientScopeRepo = _uow.Repository<ClientScope>();
            var scopeRepo = _uow.Repository<Scope>();

            var scopeIds = clientScopeRepo.Query()
                .Where(cs => cs.ClientId == client.Id)
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

        // SuperAdmin should be able to access all services/endpoints, including M2M-protected routes (e.g., SepidarGateway).
        // This is achieved by embedding all known scopes into the SuperAdmin user token.
        if (isSuperAdmin)
        {
            var scopeRepo = _uow.Repository<Scope>();
            scopeNames = scopeRepo.Query()
                .Select(s => s.Name)
                .Distinct()
                .ToList();
        }

        var sessionRepo = _uow.Repository<UserSession>();
        var session = new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            CreatedAt = DateTime.UtcNow,
            DeviceInfo = request.DeviceInfo,
            IpAddress = request.IpAddress,
            IsRevoked = false
        };
        await sessionRepo.AddAsync(session, cancellationToken);

        var tenantId = ResolveTokenTenantId(user.Id);
        var access = _jwt.GenerateAccessToken(user, session.Id, roleNames, clientId, scopeNames, out var jti, tenantId);
        var refresh = _jwt.GenerateRefreshToken();

        var rtRepo = _uow.Repository<RefreshToken>();
        await rtRepo.AddAsync(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = refresh,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = null,
            ClientId = clientId,
            JwtId = jti,
            UserSessionId = session.Id
        }, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return (user, access, refresh);
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

    private async Task EnsureRootAdminAsync(User user, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_rootAdmin.PhoneNumber))
        {
            return;
        }

        if (!string.Equals(user.PhoneNumber, _rootAdmin.PhoneNumber, StringComparison.Ordinal))
        {
            return;
        }

        var roleRepo = _uow.Repository<Role>();
        var userRoleRepo = _uow.Repository<UserRole>();

        var superAdmin = roleRepo.Query().FirstOrDefault(r => r.Name == "SuperAdmin");
        if (superAdmin is null)
        {
            superAdmin = new Role
            {
                Id = Guid.NewGuid(),
                Name = "SuperAdmin",
                DisplayName = "Super Admin",
                Description = "Full access to IdentityService.",
                CreatedAt = DateTime.UtcNow,
                IsSystem = true
            };
            await roleRepo.AddAsync(superAdmin, cancellationToken);
        }

        var hasMapping = userRoleRepo.Query().Any(ur => ur.UserId == user.Id && ur.RoleId == superAdmin.Id);
        if (!hasMapping)
        {
            await userRoleRepo.AddAsync(new UserRole
            {
                UserId = user.Id,
                RoleId = superAdmin.Id
            }, cancellationToken);
        }
        var customerRole = roleRepo.Query().FirstOrDefault(r => r.Name == "Customer");
        if (customerRole is null)
        {
            customerRole = new Role
            {
                Id = Guid.NewGuid(),
                Name = "Customer",
                DisplayName = "Customer",
                Description = "End-customer browsing catalog and managing own data.",
                CreatedAt = DateTime.UtcNow,
                IsSystem = true
            };
            await roleRepo.AddAsync(customerRole, cancellationToken);
        }

        await _uow.SaveChangesAsync(cancellationToken);
    }
}
