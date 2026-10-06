using System;
using IdentityService.Application.Features.Scopes.Dtos;
using MediatR;

namespace IdentityService.Application.Features.Scopes.Commands.CreateScope;

public sealed record CreateScopeRequest(string Name, string DisplayName, string? Description);

public sealed record CreateScopeCommand(CreateScopeRequest Request) : IRequest<ScopeDetailDto>;
