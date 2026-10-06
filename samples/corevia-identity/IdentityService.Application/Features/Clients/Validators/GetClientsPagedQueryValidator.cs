using FluentValidation;
using IdentityService.Application.Features.Clients.Queries.GetClientsPaged;

namespace IdentityService.Application.Features.Clients.Validators;

public sealed class GetClientsPagedQueryValidator : AbstractValidator<GetClientsPagedQuery>
{
    public GetClientsPagedQueryValidator()
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

