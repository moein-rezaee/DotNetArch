using IdentityService.Application.Common.Pagination;
using IdentityService.Application.Features.Scopes.Dtos;
using MediatR;

namespace IdentityService.Application.Features.Scopes.Queries.GetScopesPaged;

public sealed record GetScopesPagedQuery(int PageNumber, int PageSize) : IRequest<PagedResponse<ScopeListItemDto>>;

