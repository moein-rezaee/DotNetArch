using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IdentityService.Application.Features.Roles.Dtos;
using IdentityService.Application.Features.ScopePermissions.Dtos;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;

namespace IdentityService.Application.Features.ScopePermissions.Queries.GetScopePermissions;

public sealed class GetScopePermissionsQueryHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<GetScopePermissionsQuery, IReadOnlyCollection<ScopePermissionDto>>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public Task<IReadOnlyCollection<ScopePermissionDto>> Handle(GetScopePermissionsQuery request, CancellationToken cancellationToken)
    {
        var mappingRepo = _unitOfWork.Repository<ScopePermission>();
        var permissionRepo = _unitOfWork.Repository<Permission>();

        var mappings = mappingRepo.Query()
            .Where(sp => sp.ScopeId == request.ScopeId)
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
                return new ScopePermissionDto(permissionDto);
            })
            .ToArray();

        return Task.FromResult<IReadOnlyCollection<ScopePermissionDto>>(result);
    }
}
