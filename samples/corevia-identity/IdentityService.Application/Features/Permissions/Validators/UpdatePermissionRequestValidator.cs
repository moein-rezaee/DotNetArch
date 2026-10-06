using FluentValidation;
using IdentityService.Application.Features.Permissions.Commands.UpdatePermission;

namespace IdentityService.Application.Features.Permissions.Validators;

public sealed class UpdatePermissionRequestValidator : AbstractValidator<UpdatePermissionRequest>
{
    public UpdatePermissionRequestValidator()
    {
        RuleFor(x => x.Key)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .MaximumLength(200);
    }
}

