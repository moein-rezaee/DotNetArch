using System;
using System.Collections.Generic;
using IdentityService.Application.Features.ScopePermissions.Dtos;
using MediatR;

namespace IdentityService.Application.Features.ScopePermissions.Queries.GetScopePermissions;

public sealed record GetScopePermissionsQuery(Guid ScopeId) : IRequest<IReadOnlyCollection<ScopePermissionDto>>;

