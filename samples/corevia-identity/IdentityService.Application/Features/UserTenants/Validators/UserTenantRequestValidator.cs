using FluentValidation;
using IdentityService.Application.Features.UserTenants.Dtos;

namespace IdentityService.Application.Features.UserTenants.Validators;

public sealed class UserTenantRequestValidator : AbstractValidator<UserTenantRequest>
{
    public UserTenantRequestValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty();
    }
}

