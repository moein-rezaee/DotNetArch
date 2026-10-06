using FluentValidation;
using IdentityService.Application.Features.Identity.Validators;
using IdentityService.Application.Features.Users.Commands.CreateUser;

namespace IdentityService.Application.Features.Users.Validators;

public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty()
            .Matches(ValidationConstants.PhoneNumberPattern)
            .WithMessage("Phone number must follow the Iranian mobile format (09xxxxxxxxx).");
    }
}
