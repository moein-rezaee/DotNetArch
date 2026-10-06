using System;
using MediatR;

namespace IdentityService.Application.Features.Scopes.Commands.DeleteScope;

public sealed record DeleteScopeCommand(Guid Id) : IRequest;

