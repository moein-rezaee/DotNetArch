using System;
using MediatR;

namespace IdentityService.Application.Features.Sessions.Commands.RevokeOtherSessions;

public sealed record RevokeOtherSessionsCommand(Guid UserId, Guid? CurrentSessionId) : IRequest;

