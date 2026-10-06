using System;
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

namespace IdentityService.Application.Features.Users.Commands.UpdateUser;

public sealed class UpdateUserCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateUserCommand, UserDetailDto>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<UserDetailDto> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var userRepo = _unitOfWork.Repository<User>();
        var user = await userRepo.GetByIdAsync(request.Id, cancellationToken)
                   ?? throw new NotFoundException("User not found.");

        var phone = request.Request.PhoneNumber.Trim();
        if (!string.Equals(user.PhoneNumber, phone, StringComparison.Ordinal))
        {
            if (userRepo.Query().Any(u => u.PhoneNumber == phone))
            {
                throw new BadRequestException("Phone number already exists.", "duplicate_phone_number");
            }
        }

        user.PhoneNumber = phone;
        user.IsActive = request.Request.IsActive;
        user.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Roles and Tenants stay unchanged here; they are managed via dedicated endpoints

        var userRoleRepo = _unitOfWork.Repository<UserRole>();
        var roleRepo = _unitOfWork.Repository<Role>();
        var userTenantRepo = _unitOfWork.Repository<UserTenant>();
        var tenantRepo = _unitOfWork.Repository<Tenant>();

        var roleIds = userRoleRepo.Query()
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
