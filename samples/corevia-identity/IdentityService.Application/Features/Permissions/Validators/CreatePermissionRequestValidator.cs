using FluentValidation;
using IdentityService.Application.Features.Permissions.Commands.CreatePermission;

namespace IdentityService.Application.Features.Permissions.Validators;

public sealed class CreatePermissionRequestValidator : AbstractValidator<CreatePermissionRequest>
{
    public CreatePermissionRequestValidator()
    {
        RuleFor(x => x.Key)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .MaximumLength(200);
    }
}

