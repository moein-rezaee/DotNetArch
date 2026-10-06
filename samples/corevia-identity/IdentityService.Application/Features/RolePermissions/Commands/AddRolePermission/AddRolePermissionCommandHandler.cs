using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Corevia.Kit.ErrorHandling.Abstractions.Exceptions;
using IdentityService.Application.Features.RolePermissions.Dtos;
using IdentityService.Application.Features.Roles.Dtos;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;

namespace IdentityService.Application.Features.RolePermissions.Commands.AddRolePermission;

public sealed class AddRolePermissionCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<AddRolePermissionCommand, IReadOnlyCollection<RolePermissionDto>>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<IReadOnlyCollection<RolePermissionDto>> Handle(AddRolePermissionCommand request, CancellationToken cancellationToken)
    {
        var roleRepo = _unitOfWork.Repository<Role>();
        var permissionRepo = _unitOfWork.Repository<Permission>();
        var mappingRepo = _unitOfWork.Repository<RolePermission>();

        var role = await roleRepo.GetByIdAsync(request.RoleId, cancellationToken)
                   ?? throw new NotFoundException("Role not found.");

        var permission = await permissionRepo.GetByIdAsync(request.Request.PermissionId, cancellationToken)
                        ?? throw new NotFoundException("Permission not found.");

        var existing = mappingRepo.Query()
            .FirstOrDefault(rp => rp.RoleId == role.Id && rp.PermissionId == permission.Id);

        if (existing is null)
        {
            await mappingRepo.AddAsync(new RolePermission
            {
                RoleId = role.Id,
                PermissionId = permission.Id
            }, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var mappings = mappingRepo.Query()
            .Where(rp => rp.RoleId == role.Id)
            .ToList();

        var permissionIds = mappings.Select(m => m.PermissionId).ToList();
        var permissions = permissionRepo.Query()
            .Where(p => permissionIds.Contains(p.Id))
            .ToDictionary(p => p.Id, p => new PermissionDto(p.Id, p.Key, p.DisplayName, p.Description));

        var result = mappings
            .Select(m =>
            {
                permissions.TryGetValue(m.PermissionId, out var permissionDto);
                permissionDto ??= new PermissionDto(m.PermissionId, string.Empty, string.Empty, null);
                return new RolePermissionDto(permissionDto);
            })
            .ToArray();

        return result;
    }
}
