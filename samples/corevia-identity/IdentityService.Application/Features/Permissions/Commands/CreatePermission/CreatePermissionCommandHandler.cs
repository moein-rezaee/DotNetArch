using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Corevia.Kit.ErrorHandling.Abstractions.Exceptions;
using IdentityService.Application.Features.Roles.Dtos;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;

namespace IdentityService.Application.Features.Permissions.Commands.CreatePermission;

public sealed class CreatePermissionCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<CreatePermissionCommand, PermissionDto>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<PermissionDto> Handle(CreatePermissionCommand request, CancellationToken cancellationToken)
    {
        var permissionRepo = _unitOfWork.Repository<Permission>();

        var key = request.Request.Key.Trim();
        if (permissionRepo.Query().Any(p => p.Key == key))
        {
            throw new BadRequestException("Permission key already exists.", "duplicate_permission_key");
        }

        var permission = new Permission
        {
            Id = Guid.NewGuid(),
            Key = key,
            DisplayName = request.Request.DisplayName.Trim(),
            Description = request.Request.Description,
            CreatedAt = DateTime.UtcNow,
            IsDeprecated = false
        };

        await permissionRepo.AddAsync(permission, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new PermissionDto(permission.Id, permission.Key, permission.DisplayName, permission.Description);
    }
}

