using FluentValidation;
using IdentityService.Application.Features.RolePermissions.Dtos;

namespace IdentityService.Application.Features.RolePermissions.Validators;

public sealed class RolePermissionRequestValidator : AbstractValidator<RolePermissionRequest>
{
    public RolePermissionRequestValidator()
    {
        RuleFor(x => x.PermissionId)
            .NotEmpty();
    }
}

