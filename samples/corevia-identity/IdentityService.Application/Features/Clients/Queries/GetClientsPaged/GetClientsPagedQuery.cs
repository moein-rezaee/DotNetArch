using IdentityService.Application.Common.Pagination;
using IdentityService.Application.Features.Clients.Dtos;
using MediatR;

namespace IdentityService.Application.Features.Clients.Queries.GetClientsPaged;

public sealed record GetClientsPagedQuery(int PageNumber, int PageSize) : IRequest<PagedResponse<ClientListItemDto>>;

