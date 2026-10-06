using FluentValidation;
using IdentityService.Application.Features.Permissions.Queries.GetPermissionsPaged;

namespace IdentityService.Application.Features.Permissions.Validators;

public sealed class GetPermissionsPagedQueryValidator : AbstractValidator<GetPermissionsPagedQuery>
{
    public GetPermissionsPagedQueryValidator()
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

