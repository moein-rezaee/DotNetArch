using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Corevia.Kit.ErrorHandling.Abstractions.Exceptions;
using IdentityService.Application.Features.Roles.Dtos;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;

namespace IdentityService.Application.Features.Roles.Commands.UpdateRole;

public sealed class UpdateRoleCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateRoleCommand, RoleDetailDto>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<RoleDetailDto> Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
    {
        var roleRepo = _unitOfWork.Repository<Role>();

        var role = await roleRepo.GetByIdAsync(request.Id, cancellationToken)
                   ?? throw new NotFoundException("Role not found.");

        if (string.Equals(role.Name, "SuperAdmin", StringComparison.Ordinal))
        {
            throw new BadRequestException("SuperAdmin role cannot be modified.", "superadmin_update_forbidden");
        }

        var normalizedName = request.Request.Name.Trim();
        if (!string.Equals(role.Name, normalizedName, StringComparison.Ordinal)
            && roleRepo.Query().Any(r => r.Name == normalizedName))
        {
            throw new BadRequestException("Role name already exists.", "duplicate_role_name");
        }

        role.Name = normalizedName;
        role.DisplayName = request.Request.DisplayName.Trim();
        role.Description = request.Request.Description;
        role.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var permissions = new List<PermissionDto>();

        return new RoleDetailDto(role.Id, role.Name, role.DisplayName, role.Description, permissions);
    }
}
