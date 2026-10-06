using FluentValidation;
using IdentityService.Application.Features.Users.Queries.GetUsersPaged;

namespace IdentityService.Application.Features.Users.Validators;

public sealed class GetUsersPagedQueryValidator : AbstractValidator<GetUsersPagedQuery>
{
    public GetUsersPagedQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThan(0)
            .WithMessage("Page number must be greater than 0.")
            .WithErrorCode("InvalidPageNumber");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Page size must be between 1 and 100.")
            .WithErrorCode("InvalidPageSize");
    }
}
