using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IdentityService.Application.Features.Sessions.Dtos;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;

namespace IdentityService.Application.Features.Sessions.Queries.GetCurrentUserSessions;

public sealed class GetCurrentUserSessionsQueryHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<GetCurrentUserSessionsQuery, IReadOnlyCollection<SessionDto>>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public Task<IReadOnlyCollection<SessionDto>> Handle(GetCurrentUserSessionsQuery request, CancellationToken cancellationToken)
    {
        var repo = _unitOfWork.Repository<UserSession>();

        var sessions = repo.Query()
            .Where(s => s.UserId == request.UserId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new SessionDto(s.Id, s.CreatedAt, s.EndedAt, s.DeviceInfo, s.IpAddress, s.IsRevoked))
            .ToArray();

        return Task.FromResult<IReadOnlyCollection<SessionDto>>(sessions);
    }
}

