using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;

namespace IdentityService.Application.Features.ScopePermissions.Commands.RemoveScopePermission;

public sealed class RemoveScopePermissionCommandHandler(IUnitOfWork unitOfWork) : IRequestHandler<RemoveScopePermissionCommand>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task Handle(RemoveScopePermissionCommand request, CancellationToken cancellationToken)
    {
        var mappingRepo = _unitOfWork.Repository<ScopePermission>();

        var mapping = mappingRepo.Query()
            .FirstOrDefault(sp => sp.ScopeId == request.ScopeId && sp.PermissionId == request.PermissionId);

        if (mapping is not null)
        {
            mappingRepo.Remove(mapping);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}

