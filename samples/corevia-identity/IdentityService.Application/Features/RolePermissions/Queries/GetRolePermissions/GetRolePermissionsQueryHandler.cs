using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IdentityService.Application.Features.RolePermissions.Dtos;
using IdentityService.Application.Features.Roles.Dtos;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;

namespace IdentityService.Application.Features.RolePermissions.Queries.GetRolePermissions;

public sealed class GetRolePermissionsQueryHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<GetRolePermissionsQuery, IReadOnlyCollection<RolePermissionDto>>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public Task<IReadOnlyCollection<RolePermissionDto>> Handle(GetRolePermissionsQuery request, CancellationToken cancellationToken)
    {
        var mappingRepo = _unitOfWork.Repository<RolePermission>();
        var permissionRepo = _unitOfWork.Repository<Permission>();

        var mappings = mappingRepo.Query()
            .Where(rp => rp.RoleId == request.RoleId)
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

        return Task.FromResult<IReadOnlyCollection<RolePermissionDto>>(result);
    }
}
