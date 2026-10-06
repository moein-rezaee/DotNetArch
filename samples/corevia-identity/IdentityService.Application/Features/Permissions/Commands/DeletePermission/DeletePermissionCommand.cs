using System;
using MediatR;

namespace IdentityService.Application.Features.Permissions.Commands.DeletePermission;

public sealed record DeletePermissionCommand(Guid Id, string? Reason) : IRequest;

