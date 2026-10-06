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

namespace IdentityService.Application.Features.Users.Commands.CreateUser;

public sealed class CreateUserCommandHandler(IUnitOfWork unitOfWork)
    : IRequestHandler<CreateUserCommand, UserDetailDto>
{
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<UserDetailDto> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var userRepo = _unitOfWork.Repository<User>();

        var phone = request.Request.PhoneNumber.Trim();
        if (userRepo.Query().Any(u => u.PhoneNumber == phone))
        {
            throw new BadRequestException("Phone number already exists.", "duplicate_phone_number");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            PhoneNumber = phone,
            CreatedAt = DateTime.UtcNow,
            IsActive = request.Request.IsActive
        };

        await userRepo.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new UserDetailDto(
            user.Id,
            user.PhoneNumber,
            user.CreatedAt,
            user.UpdatedAt,
            user.IsActive,
            Array.Empty<RoleListItemDto>(),
            Array.Empty<TenantListItemDto>());
    }
}
