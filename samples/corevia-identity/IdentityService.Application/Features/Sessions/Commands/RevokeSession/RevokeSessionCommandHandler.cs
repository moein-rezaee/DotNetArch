using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Corevia.Kit.ErrorHandling.Abstractions.Exceptions;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;

namespace IdentityService.Application.Features.Sessions.Commands.RevokeSession;

public sealed class RevokeSessionCommandHandler(IUnitOfWork unitOfWork) : IRequestHandler<RevokeSessionCommand>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task Handle(RevokeSessionCommand request, CancellationToken cancellationToken)
    {
        var sessionRepo = _unitOfWork.Repository<UserSession>();
        var refreshRepo = _unitOfWork.Repository<RefreshToken>();

        var session = await sessionRepo.GetByIdAsync(request.SessionId, cancellationToken);
        if (session is null || session.UserId != request.UserId)
        {
            throw new NotFoundException("Session not found.");
        }

        if (!session.IsRevoked)
        {
            session.IsRevoked = true;
            session.EndedAt = DateTime.UtcNow;

            var tokens = refreshRepo.Query()
            .Where(rt => rt.UserSessionId == session.Id && rt.RevokedAt == null && (!rt.ExpiresAt.HasValue || rt.ExpiresAt > DateTime.UtcNow))
                .ToList();

            foreach (var t in tokens)
            {
                t.RevokedAt = DateTime.UtcNow;
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
