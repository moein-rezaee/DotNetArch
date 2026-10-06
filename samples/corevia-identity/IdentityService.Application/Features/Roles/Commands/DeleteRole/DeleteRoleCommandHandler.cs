using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Corevia.Kit.ErrorHandling.Abstractions.Exceptions;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;

namespace IdentityService.Application.Features.Roles.Commands.DeleteRole;

public sealed class DeleteRoleCommandHandler(IUnitOfWork unitOfWork) : IRequestHandler<DeleteRoleCommand>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
    {
        var roleRepo = _unitOfWork.Repository<Role>();
        var role = await roleRepo.GetByIdAsync(request.Id, cancellationToken)
                   ?? throw new NotFoundException("Role not found.");

        if (string.Equals(role.Name, "SuperAdmin", System.StringComparison.Ordinal))
        {
            throw new BadRequestException("SuperAdmin role cannot be deleted.", "superadmin_delete_forbidden");
        }

        var userRoleRepo = _unitOfWork.Repository<UserRole>();
        var anyUser = userRoleRepo.Query().Any(ur => ur.RoleId == role.Id);
        if (anyUser)
        {
            throw new BadRequestException("Role is assigned to users and cannot be deleted.", "role_has_users");
        }

        var rolePermissionRepo = _unitOfWork.Repository<RolePermission>();
        var permissions = rolePermissionRepo.Query().Where(rp => rp.RoleId == role.Id).ToList();
        foreach (var rp in permissions)
        {
            rolePermissionRepo.Remove(rp);
        }

        roleRepo.Remove(role);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
