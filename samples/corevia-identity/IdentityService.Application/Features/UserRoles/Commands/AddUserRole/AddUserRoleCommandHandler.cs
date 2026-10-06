using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Corevia.Kit.ErrorHandling.Abstractions.Exceptions;
using IdentityService.Application.Features.Identity.Options;
using IdentityService.Application.Features.Roles.Dtos;
using IdentityService.Application.Features.UserRoles.Dtos;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Options;

namespace IdentityService.Application.Features.UserRoles.Commands.AddUserRole;

public sealed class AddUserRoleCommandHandler(IUnitOfWork unitOfWork, IOptions<RootAdminOptions> rootAdminOptions)
    : IRequestHandler<AddUserRoleCommand, IReadOnlyCollection<UserRoleDto>>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly RootAdminOptions _rootAdmin = rootAdminOptions.Value;

    public async Task<IReadOnlyCollection<UserRoleDto>> Handle(AddUserRoleCommand request, CancellationToken cancellationToken)
    {
        var userRepo = _unitOfWork.Repository<User>();
        var roleRepo = _unitOfWork.Repository<Role>();
        var mappingRepo = _unitOfWork.Repository<UserRole>();

        var user = await userRepo.GetByIdAsync(request.UserId, cancellationToken)
                   ?? throw new NotFoundException("User not found.");

        var role = await roleRepo.GetByIdAsync(request.Request.RoleId, cancellationToken)
                   ?? throw new NotFoundException("Role not found.");

        if (string.Equals(role.Name, "SuperAdmin", StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(_rootAdmin.PhoneNumber) ||
                !string.Equals(user.PhoneNumber, _rootAdmin.PhoneNumber, StringComparison.Ordinal))
            {
                throw new BadRequestException(
                    "SuperAdmin role can only be assigned to the configured root admin user.",
                    "superadmin_assignment_forbidden");
            }
        }

        var existing = mappingRepo.Query()
            .FirstOrDefault(ur => ur.UserId == user.Id && ur.RoleId == role.Id);

        if (existing is null)
        {
            await mappingRepo.AddAsync(new UserRole
            {
                UserId = user.Id,
                RoleId = role.Id
            }, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var mappings = mappingRepo.Query()
            .Where(ur => ur.UserId == user.Id)
            .ToList();

        var roleIds = mappings.Select(m => m.RoleId).ToList();
        var roles = roleRepo.Query()
            .Where(r => roleIds.Contains(r.Id))
            .ToDictionary(r => r.Id, r => new RoleListItemDto(r.Id, r.Name, r.DisplayName, r.Description));

        var result = mappings
            .Select(m =>
            {
                roles.TryGetValue(m.RoleId, out var roleDto);
                roleDto ??= new RoleListItemDto(m.RoleId, string.Empty, string.Empty, null);
                return new UserRoleDto(roleDto);
            })
            .ToArray();

        return result;
    }
}
