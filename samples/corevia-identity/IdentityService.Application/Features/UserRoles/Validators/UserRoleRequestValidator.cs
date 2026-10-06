using FluentValidation;
using IdentityService.Application.Features.UserRoles.Dtos;

namespace IdentityService.Application.Features.UserRoles.Validators;

public sealed class UserRoleRequestValidator : AbstractValidator<UserRoleRequest>
{
    public UserRoleRequestValidator()
    {
        RuleFor(x => x.RoleId)
            .NotEmpty();
    }
}

