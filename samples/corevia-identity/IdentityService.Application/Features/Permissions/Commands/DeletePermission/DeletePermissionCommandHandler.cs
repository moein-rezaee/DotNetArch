using System.Threading;
using System.Threading.Tasks;
using Corevia.Kit.ErrorHandling.Abstractions.Exceptions;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;

namespace IdentityService.Application.Features.Permissions.Commands.DeletePermission;

public sealed class DeletePermissionCommandHandler(IUnitOfWork unitOfWork) : IRequestHandler<DeletePermissionCommand>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task Handle(DeletePermissionCommand request, CancellationToken cancellationToken)
    {
        var permissionRepo = _unitOfWork.Repository<Permission>();

        var permission = await permissionRepo.GetByIdAsync(request.Id, cancellationToken);
        if (permission is null)
        {
            throw new NotFoundException("Permission not found.");
        }

        // Soft-delete: mark as deprecated and keep relations for history
        permission.IsDeprecated = true;
        permission.DeprecationReason = request.Reason ?? "deleted_via_api";
        permission.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
