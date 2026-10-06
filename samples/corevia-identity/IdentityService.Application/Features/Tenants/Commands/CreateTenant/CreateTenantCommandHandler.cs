using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Corevia.Kit.ErrorHandling.Abstractions.Exceptions;
using IdentityService.Application.Features.Tenants.Dtos;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;

namespace IdentityService.Application.Features.Tenants.Commands.CreateTenant;

public sealed class CreateTenantCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<CreateTenantCommand, TenantDetailDto>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<TenantDetailDto> Handle(CreateTenantCommand request, CancellationToken cancellationToken)
    {
        var tenantRepo = _unitOfWork.Repository<Tenant>();

        var normalizedName = request.Request.Name.Trim();
        if (tenantRepo.Query().Any(t => t.Name == normalizedName))
        {
            throw new BadRequestException("Tenant name already exists.", "duplicate_tenant_name");
        }

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = normalizedName,
            DisplayName = request.Request.DisplayName.Trim(),
            IsActive = request.Request.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        await tenantRepo.AddAsync(tenant, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new TenantDetailDto(tenant.Id, tenant.Name, tenant.DisplayName, tenant.IsActive, tenant.CreatedAt, tenant.UpdatedAt);
    }
}

