using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Corevia.Kit.ErrorHandling.Abstractions.Exceptions;
using IdentityService.Application.Features.Roles.Dtos;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;

namespace IdentityService.Application.Features.Roles.Queries.GetRoleById;

public sealed class GetRoleByIdQueryHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<GetRoleByIdQuery, RoleDetailDto>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public Task<RoleDetailDto> Handle(GetRoleByIdQuery request, CancellationToken cancellationToken)
    {
        var role = _unitOfWork.Repository<Role>().Query().FirstOrDefault(r => r.Id == request.Id);
        if (role is null)
        {
            throw new NotFoundException("Role not found.");
        }

        var rolePermissionsRepo = _unitOfWork.Repository<RolePermission>();
        var permissionRepo = _unitOfWork.Repository<Permission>();

        var permissionIds = rolePermissionsRepo.Query()
            .Where(rp => rp.RoleId == role.Id)
            .Select(rp => rp.PermissionId)
            .ToList();

        var permissions = permissionRepo.Query()
            .Where(p => permissionIds.Contains(p.Id))
            .Select(p => new PermissionDto(p.Id, p.Key, p.DisplayName, p.Description))
            .ToList();

        var dto = new RoleDetailDto(role.Id, role.Name, role.DisplayName, role.Description, permissions);
        return Task.FromResult(dto);
    }
}

