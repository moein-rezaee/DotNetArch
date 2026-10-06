using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Corevia.Kit.ErrorHandling.Abstractions.Exceptions;
using IdentityService.Application.Features.Roles.Dtos;
using IdentityService.Application.Features.Tenants.Dtos;
using IdentityService.Application.Features.Users.Dtos;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;

namespace IdentityService.Application.Features.Users.Queries.GetUserById;

public sealed class GetUserByIdQueryHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<GetUserByIdQuery, UserDetailDto>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<UserDetailDto> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await _unitOfWork.Repository<User>().GetByIdAsync(request.Id, cancellationToken)
                   ?? throw new NotFoundException("User not found.");

        var userRolesRepo = _unitOfWork.Repository<UserRole>();
        var roleRepo = _unitOfWork.Repository<Role>();
        var userTenantRepo = _unitOfWork.Repository<UserTenant>();
        var tenantRepo = _unitOfWork.Repository<Tenant>();

        var roleIds = userRolesRepo.Query()
            .Where(ur => ur.UserId == user.Id)
            .Select(ur => ur.RoleId)
            .ToList();

        var roles = roleRepo.Query()
            .Where(r => roleIds.Contains(r.Id))
            .Select(r => new RoleListItemDto(r.Id, r.Name, r.DisplayName, r.Description))
            .ToList();

        var tenantIds = userTenantRepo.Query()
            .Where(ut => ut.UserId == user.Id)
            .Select(ut => ut.TenantId)
            .ToList();

        var tenants = tenantRepo.Query()
            .Where(t => tenantIds.Contains(t.Id))
            .Select(t => new TenantListItemDto(t.Id, t.Name, t.DisplayName, t.IsActive))
            .ToList();

        return new UserDetailDto(user.Id, user.PhoneNumber, user.CreatedAt, user.UpdatedAt, user.IsActive, roles, tenants);
    }
}
