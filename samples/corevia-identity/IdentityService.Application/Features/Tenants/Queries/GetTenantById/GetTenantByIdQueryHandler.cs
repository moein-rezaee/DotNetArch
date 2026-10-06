using System.Threading;
using System.Threading.Tasks;
using Corevia.Kit.ErrorHandling.Abstractions.Exceptions;
using IdentityService.Application.Features.Tenants.Dtos;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;

namespace IdentityService.Application.Features.Tenants.Queries.GetTenantById;

public sealed class GetTenantByIdQueryHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<GetTenantByIdQuery, TenantDetailDto>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<TenantDetailDto> Handle(GetTenantByIdQuery request, CancellationToken cancellationToken)
    {
        var tenant = await _unitOfWork.Repository<Tenant>().GetByIdAsync(request.Id, cancellationToken)
                     ?? throw new NotFoundException("Tenant not found.");

        return new TenantDetailDto(tenant.Id, tenant.Name, tenant.DisplayName, tenant.IsActive, tenant.CreatedAt, tenant.UpdatedAt);
    }
}

