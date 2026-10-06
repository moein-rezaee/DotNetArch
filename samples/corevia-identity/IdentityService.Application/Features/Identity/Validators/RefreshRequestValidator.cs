using IdentityService.Application.Features.Identity.Models;
using FluentValidation;

namespace IdentityService.Application.Features.Identity.Validators;

public class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    private const int MinimumTokenLength = 32;
    private const int MaximumTokenLength = 512;

    public RefreshRequestValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty()
            .MinimumLength(MinimumTokenLength)
            .MaximumLength(MaximumTokenLength);
    }
}
