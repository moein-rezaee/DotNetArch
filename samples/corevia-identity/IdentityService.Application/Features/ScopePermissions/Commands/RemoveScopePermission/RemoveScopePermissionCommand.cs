using System;
using MediatR;

namespace IdentityService.Application.Features.ScopePermissions.Commands.RemoveScopePermission;

public sealed record RemoveScopePermissionCommand(Guid ScopeId, Guid PermissionId) : IRequest;

