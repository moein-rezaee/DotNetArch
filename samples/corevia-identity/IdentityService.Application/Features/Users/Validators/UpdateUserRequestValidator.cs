using FluentValidation;
using IdentityService.Application.Features.Users.Commands.UpdateUser;
using IdentityService.Application.Features.Identity.Validators;

namespace IdentityService.Application.Features.Users.Validators;

public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty()
            .Matches(ValidationConstants.PhoneNumberPattern)
            .WithMessage("Phone number must follow the Iranian mobile format (09xxxxxxxxx).");
    }
}

