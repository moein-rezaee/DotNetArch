using System;
using MediatR;

namespace IdentityService.Application.Features.UserTenants.Commands.RemoveUserTenant;

public sealed record RemoveUserTenantCommand(Guid UserId, Guid TenantId) : IRequest;

