using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Corevia.Kit.ErrorHandling.Abstractions.Exceptions;
using IdentityService.Application.Features.Roles.Dtos;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;

namespace IdentityService.Application.Features.Permissions.Commands.UpdatePermission;

public sealed class UpdatePermissionCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<UpdatePermissionCommand, PermissionDto>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<PermissionDto> Handle(UpdatePermissionCommand request, CancellationToken cancellationToken)
    {
        var permissionRepo = _unitOfWork.Repository<Permission>();

        var permission = await permissionRepo.GetByIdAsync(request.Id, cancellationToken)
                         ?? throw new NotFoundException("Permission not found.");

        var key = request.Request.Key.Trim();
        if (!string.Equals(permission.Key, key, StringComparison.Ordinal) &&
            permissionRepo.Query().Any(p => p.Key == key))
        {
            throw new BadRequestException("Permission key already exists.", "duplicate_permission_key");
        }

        permission.Key = key;
        permission.DisplayName = request.Request.DisplayName.Trim();
        permission.Description = request.Request.Description;
        permission.IsDeprecated = request.Request.IsDeprecated;
        permission.DeprecationReason = request.Request.IsDeprecated ? request.Request.DeprecationReason : null;
        permission.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new PermissionDto(permission.Id, permission.Key, permission.DisplayName, permission.Description);
    }
}

