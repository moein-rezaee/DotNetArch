using IdentityService.Application.Features.Identity.Models;
using FluentValidation;

namespace IdentityService.Application.Features.Identity.Validators;

public class SendCodeRequestValidator : AbstractValidator<SendCodeRequest>
{
    public SendCodeRequestValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty()
            .Matches(ValidationConstants.PhoneNumberPattern)
            .WithMessage("Phone number must follow the Iranian mobile format (09xxxxxxxxx).");
    }
}
