using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IdentityService.Application.Features.Roles.Dtos;
using IdentityService.Application.Features.UserRoles.Dtos;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using MediatR;

namespace IdentityService.Application.Features.UserRoles.Queries.GetUserRoles;

public sealed class GetUserRolesQueryHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<GetUserRolesQuery, IReadOnlyCollection<UserRoleDto>>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public Task<IReadOnlyCollection<UserRoleDto>> Handle(GetUserRolesQuery request, CancellationToken cancellationToken)
    {
        var userRoleRepo = _unitOfWork.Repository<UserRole>();
        var roleRepo = _unitOfWork.Repository<Role>();

        var mappings = userRoleRepo.Query()
            .Where(ur => ur.UserId == request.UserId)
            .ToList();

        var roleIds = mappings.Select(m => m.RoleId).ToList();
        var roles = roleRepo.Query()
            .Where(r => roleIds.Contains(r.Id))
            .ToDictionary(r => r.Id, r => new RoleListItemDto(r.Id, r.Name, r.DisplayName, r.Description));

        var result = mappings
            .Select(m =>
            {
                roles.TryGetValue(m.RoleId, out var roleDto);
                roleDto ??= new RoleListItemDto(m.RoleId, string.Empty, string.Empty, null);
                return new UserRoleDto(roleDto);
            })
            .ToArray();

        return Task.FromResult<IReadOnlyCollection<UserRoleDto>>(result);
    }
}
