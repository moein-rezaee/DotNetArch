using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;

namespace IdentityService.Application.Features.Identity.Commands.Logout;

public class LogoutCommandHandler(IUnitOfWork uow) : IRequestHandler<LogoutCommand, Unit>
{
    private readonly IUnitOfWork _uow = uow;
    public async Task<Unit> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var refreshRepo = _uow.Repository<RefreshToken>();
        var sessionRepo = _uow.Repository<UserSession>();

        var token = await refreshRepo.FirstOrDefaultAsync(x => x.Token == request.Request.RefreshToken && x.RevokedAt == null, cancellationToken);
        if (token != null)
        {
            token.RevokedAt = DateTime.UtcNow;

            if (token.UserSessionId.HasValue)
            {
                var session = await sessionRepo.GetByIdAsync(token.UserSessionId.Value, cancellationToken);
                if (session is not null && !session.IsRevoked)
                {
                    session.IsRevoked = true;
                    session.EndedAt = DateTime.UtcNow;
                }
            }

            await _uow.SaveChangesAsync(cancellationToken);
        }
        return Unit.Value;
    }
}
