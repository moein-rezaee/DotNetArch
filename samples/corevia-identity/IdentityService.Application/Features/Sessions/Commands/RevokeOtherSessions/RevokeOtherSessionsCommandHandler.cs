using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;

namespace IdentityService.Application.Features.Sessions.Commands.RevokeOtherSessions;

public sealed class RevokeOtherSessionsCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<RevokeOtherSessionsCommand>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task Handle(RevokeOtherSessionsCommand request, CancellationToken cancellationToken)
    {
        var sessionRepo = _unitOfWork.Repository<UserSession>();
        var refreshRepo = _unitOfWork.Repository<RefreshToken>();

        var sessions = sessionRepo.Query()
            .Where(s => s.UserId == request.UserId && (!request.CurrentSessionId.HasValue || s.Id != request.CurrentSessionId.Value))
            .ToList();

        foreach (var session in sessions.Where(s => !s.IsRevoked))
        {
            session.IsRevoked = true;
            session.EndedAt = DateTime.UtcNow;
        }

        var sessionIds = sessions.Select(s => s.Id).ToList();
        var tokens = refreshRepo.Query()
            .Where(rt => rt.UserSessionId.HasValue && sessionIds.Contains(rt.UserSessionId.Value) && rt.RevokedAt == null && (!rt.ExpiresAt.HasValue || rt.ExpiresAt > DateTime.UtcNow))
            .ToList();

        foreach (var t in tokens)
        {
            t.RevokedAt = DateTime.UtcNow;
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
