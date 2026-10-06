using System;
using MediatR;

namespace IdentityService.Application.Features.Tenants.Commands.DeleteTenant;

public sealed record DeleteTenantCommand(Guid Id) : IRequest;

