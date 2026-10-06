using System;
using IdentityService.Application.Features.Roles.Dtos;
using MediatR;

namespace IdentityService.Application.Features.Permissions.Queries.GetPermissionById;

public sealed record GetPermissionByIdQuery(Guid Id) : IRequest<PermissionDto>;

