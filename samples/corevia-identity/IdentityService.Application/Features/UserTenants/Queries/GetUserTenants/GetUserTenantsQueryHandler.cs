using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IdentityService.Application.Features.Tenants.Dtos;
using IdentityService.Application.Features.UserTenants.Dtos;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;

namespace IdentityService.Application.Features.UserTenants.Queries.GetUserTenants;

public sealed class GetUserTenantsQueryHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<GetUserTenantsQuery, IReadOnlyCollection<UserTenantDto>>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public Task<IReadOnlyCollection<UserTenantDto>> Handle(GetUserTenantsQuery request, CancellationToken cancellationToken)
    {
        var userTenantRepo = _unitOfWork.Repository<UserTenant>();
        var tenantRepo = _unitOfWork.Repository<Tenant>();

        var mappings = userTenantRepo.Query()
            .Where(ut => ut.UserId == request.UserId)
            .ToList();

        var tenantIds = mappings.Select(m => m.TenantId).ToList();

        var tenants = tenantRepo.Query()
            .Where(t => tenantIds.Contains(t.Id))
            .ToDictionary(t => t.Id, t => new TenantListItemDto(t.Id, t.Name, t.DisplayName, t.IsActive));

        var result = mappings
            .Select(m =>
            {
                tenants.TryGetValue(m.TenantId, out var tenantDto);
                tenantDto ??= new TenantListItemDto(m.TenantId, string.Empty, string.Empty, false);
                return new UserTenantDto(tenantDto, m.IsDefault);
            })
            .ToArray();

        return Task.FromResult<IReadOnlyCollection<UserTenantDto>>(result);
    }
}
