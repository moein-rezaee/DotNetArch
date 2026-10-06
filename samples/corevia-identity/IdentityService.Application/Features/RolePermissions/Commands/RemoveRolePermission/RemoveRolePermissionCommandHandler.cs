using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;

namespace IdentityService.Application.Features.RolePermissions.Commands.RemoveRolePermission;

public sealed class RemoveRolePermissionCommandHandler(IUnitOfWork unitOfWork) : IRequestHandler<RemoveRolePermissionCommand>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task Handle(RemoveRolePermissionCommand request, CancellationToken cancellationToken)
    {
        var mappingRepo = _unitOfWork.Repository<RolePermission>();

        var mapping = mappingRepo.Query()
            .FirstOrDefault(rp => rp.RoleId == request.RoleId && rp.PermissionId == request.PermissionId);

        if (mapping is not null)
        {
            mappingRepo.Remove(mapping);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}

