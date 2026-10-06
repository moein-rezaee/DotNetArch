using IdentityService.Application.Features.Identity.Models;
using FluentValidation;

namespace IdentityService.Application.Features.Identity.Validators;

public class VerifyCodeRequestValidator : AbstractValidator<VerifyCodeRequest>
{
    public VerifyCodeRequestValidator()
    {
        RuleFor(x => x.PhoneNumber)
            .NotEmpty()
            .Matches(ValidationConstants.PhoneNumberPattern)
            .WithMessage("Phone number must follow the Iranian mobile format (09xxxxxxxxx).");

        RuleFor(x => x.Code)
            .NotEmpty()
            .Length(5)
            .Matches(@"^\d+$")
            .WithMessage("Code must be a 5 digit numeric value.");
    }
}
