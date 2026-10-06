using System;
using System.Collections.Generic;
using IdentityService.Application.Features.Sessions.Dtos;
using MediatR;

namespace IdentityService.Application.Features.Sessions.Queries.GetCurrentUserSessions;

public sealed record GetCurrentUserSessionsQuery(Guid UserId) : IRequest<IReadOnlyCollection<SessionDto>>;

