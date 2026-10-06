using IdentityService.Application.Features.Identity.Models;
using FluentValidation;

namespace IdentityService.Application.Features.Identity.Validators;

public class LogoutRequestValidator : AbstractValidator<LogoutRequest>
{
    private const int MinimumTokenLength = 32;
    private const int MaximumTokenLength = 512;

    public LogoutRequestValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty()
            .MinimumLength(MinimumTokenLength)
            .MaximumLength(MaximumTokenLength);
    }
}
