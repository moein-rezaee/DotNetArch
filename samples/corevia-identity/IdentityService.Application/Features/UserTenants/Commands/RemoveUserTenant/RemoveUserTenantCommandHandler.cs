using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;

namespace IdentityService.Application.Features.UserTenants.Commands.RemoveUserTenant;

public sealed class RemoveUserTenantCommandHandler(IUnitOfWork unitOfWork) : IRequestHandler<RemoveUserTenantCommand>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task Handle(RemoveUserTenantCommand request, CancellationToken cancellationToken)
    {
        var mappingRepo = _unitOfWork.Repository<UserTenant>();

        var mapping = mappingRepo.Query()
            .Where(ut => ut.UserId == request.UserId && ut.TenantId == request.TenantId)
            .FirstOrDefault();

        if (mapping is not null)
        {
            mappingRepo.Remove(mapping);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}

