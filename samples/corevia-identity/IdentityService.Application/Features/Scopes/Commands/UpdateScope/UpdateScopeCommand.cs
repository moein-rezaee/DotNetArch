using System;
using IdentityService.Application.Features.Scopes.Dtos;
using MediatR;

namespace IdentityService.Application.Features.Scopes.Commands.UpdateScope;

public sealed record UpdateScopeRequest(string Name, string DisplayName, string? Description);

public sealed record UpdateScopeCommand(Guid Id, UpdateScopeRequest Request) : IRequest<ScopeDetailDto>;
