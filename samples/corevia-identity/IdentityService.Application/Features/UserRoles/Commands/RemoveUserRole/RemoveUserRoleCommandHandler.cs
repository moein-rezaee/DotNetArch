using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Corevia.Kit.ErrorHandling.Abstractions.Exceptions;
using IdentityService.Application.Features.Identity.Options;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Options;

namespace IdentityService.Application.Features.UserRoles.Commands.RemoveUserRole;

public sealed class RemoveUserRoleCommandHandler(IUnitOfWork unitOfWork, IOptions<RootAdminOptions> rootAdminOptions)
    : IRequestHandler<RemoveUserRoleCommand>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly RootAdminOptions _rootAdmin = rootAdminOptions.Value;

    public async Task Handle(RemoveUserRoleCommand request, CancellationToken cancellationToken)
    {
        var mappingRepo = _unitOfWork.Repository<UserRole>();

        var mapping = mappingRepo.Query()
            .FirstOrDefault(ur => ur.UserId == request.UserId && ur.RoleId == request.RoleId);

        if (mapping is not null)
        {
            var roleRepo = _unitOfWork.Repository<Role>();
            var userRepo = _unitOfWork.Repository<User>();

            var role = await roleRepo.GetByIdAsync(request.RoleId, cancellationToken);
            var user = await userRepo.GetByIdAsync(request.UserId, cancellationToken);

            if (role is not null
                && string.Equals(role.Name, "SuperAdmin", System.StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(_rootAdmin.PhoneNumber)
                && user is not null
                && string.Equals(user.PhoneNumber, _rootAdmin.PhoneNumber, System.StringComparison.Ordinal))
            {
                throw new BadRequestException(
                    "SuperAdmin role cannot be removed from the root admin user.",
                    "superadmin_unassign_forbidden");
            }

            mappingRepo.Remove(mapping);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
