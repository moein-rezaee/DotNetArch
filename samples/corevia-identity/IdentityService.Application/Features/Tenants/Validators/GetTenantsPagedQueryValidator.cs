using FluentValidation;
using IdentityService.Application.Features.Tenants.Queries.GetTenantsPaged;

namespace IdentityService.Application.Features.Tenants.Validators;

public sealed class GetTenantsPagedQueryValidator : AbstractValidator<GetTenantsPagedQuery>
{
    public GetTenantsPagedQueryValidator()
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

