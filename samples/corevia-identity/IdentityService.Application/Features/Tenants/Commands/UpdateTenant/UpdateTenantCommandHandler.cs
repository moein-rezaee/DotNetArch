using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Corevia.Kit.ErrorHandling.Abstractions.Exceptions;
using IdentityService.Application.Features.Tenants.Dtos;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;

namespace IdentityService.Application.Features.Tenants.Commands.UpdateTenant;

public sealed class UpdateTenantCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateTenantCommand, TenantDetailDto>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<TenantDetailDto> Handle(UpdateTenantCommand request, CancellationToken cancellationToken)
    {
        var tenantRepo = _unitOfWork.Repository<Tenant>();

        var tenant = await tenantRepo.GetByIdAsync(request.Id, cancellationToken)
                     ?? throw new NotFoundException("Tenant not found.");

        var normalizedName = request.Request.Name.Trim();
        if (!string.Equals(tenant.Name, normalizedName, StringComparison.Ordinal) &&
            tenantRepo.Query().Any(t => t.Name == normalizedName))
        {
            throw new BadRequestException("Tenant name already exists.", "duplicate_tenant_name");
        }

        tenant.Name = normalizedName;
        tenant.DisplayName = request.Request.DisplayName.Trim();
        tenant.IsActive = request.Request.IsActive;
        tenant.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new TenantDetailDto(tenant.Id, tenant.Name, tenant.DisplayName, tenant.IsActive, tenant.CreatedAt, tenant.UpdatedAt);
    }
}

