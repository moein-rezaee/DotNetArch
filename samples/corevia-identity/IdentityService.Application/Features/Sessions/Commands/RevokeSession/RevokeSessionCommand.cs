using System;
using MediatR;

namespace IdentityService.Application.Features.Sessions.Commands.RevokeSession;

public sealed record RevokeSessionCommand(Guid UserId, Guid SessionId) : IRequest;

