using System;
using IdentityService.Application.Features.UserTenants.Dtos;
using MediatR;

namespace IdentityService.Application.Features.UserTenants.Commands.AddUserTenant;

public sealed record AddUserTenantCommand(Guid UserId, UserTenantRequest Request) : IRequest<IReadOnlyCollection<UserTenantDto>>;

