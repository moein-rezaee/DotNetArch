using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Corevia.Kit.ErrorHandling.Abstractions.Exceptions;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;

namespace IdentityService.Application.Features.Tenants.Commands.DeleteTenant;

public sealed class DeleteTenantCommandHandler(IUnitOfWork unitOfWork) : IRequestHandler<DeleteTenantCommand>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task Handle(DeleteTenantCommand request, CancellationToken cancellationToken)
    {
        var tenantRepo = _unitOfWork.Repository<Tenant>();
        var tenant = await tenantRepo.GetByIdAsync(request.Id, cancellationToken);
        if (tenant is null)
        {
            throw new NotFoundException("Tenant not found.");
        }

        var userTenantRepo = _unitOfWork.Repository<UserTenant>();
        var anyUser = userTenantRepo.Query().Any(ut => ut.TenantId == tenant.Id);
        if (anyUser)
        {
            throw new BadRequestException("Tenant has users and cannot be deleted.", "tenant_has_users");
        }

        tenantRepo.Remove(tenant);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

