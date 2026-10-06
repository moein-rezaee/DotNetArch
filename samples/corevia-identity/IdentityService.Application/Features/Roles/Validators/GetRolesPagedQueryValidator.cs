using FluentValidation;
using IdentityService.Application.Features.Roles.Queries.GetRolesPaged;

namespace IdentityService.Application.Features.Roles.Validators;

public sealed class GetRolesPagedQueryValidator : AbstractValidator<GetRolesPagedQuery>
{
    public GetRolesPagedQueryValidator()
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

