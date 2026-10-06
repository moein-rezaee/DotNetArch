using System;
using System.Collections.Generic;
using IdentityService.Application.Features.ScopePermissions.Dtos;
using MediatR;

namespace IdentityService.Application.Features.ScopePermissions.Commands.AddScopePermission;

public sealed record AddScopePermissionCommand(Guid ScopeId, ScopePermissionRequest Request) : IRequest<IReadOnlyCollection<ScopePermissionDto>>;

