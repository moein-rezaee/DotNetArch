using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Corevia.Kit.ErrorHandling.Abstractions.Exceptions;
using IdentityService.Application.Features.Roles.Dtos;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;

namespace IdentityService.Application.Features.Roles.Commands.CreateRole;

public sealed class CreateRoleCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<CreateRoleCommand, RoleDetailDto>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<RoleDetailDto> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        var roleRepo = _unitOfWork.Repository<Role>();
        var permissionRepo = _unitOfWork.Repository<Permission>();

        var normalizedName = request.Request.Name.Trim();
        if (roleRepo.Query().Any(r => r.Name == normalizedName))
        {
            throw new BadRequestException("Role name already exists.", "duplicate_role_name");
        }

        var role = new Role
        {
            Id = Guid.NewGuid(),
            Name = normalizedName,
            DisplayName = request.Request.DisplayName.Trim(),
            Description = request.Request.Description,
            CreatedAt = DateTime.UtcNow
        };

        await roleRepo.AddAsync(role, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // RolePermissions از طریق اندپوینت‌های جداگانه مدیریت می‌شوند
        var permissions = new List<PermissionDto>();

        return new RoleDetailDto(role.Id, role.Name, role.DisplayName, role.Description, permissions);
    }
}
