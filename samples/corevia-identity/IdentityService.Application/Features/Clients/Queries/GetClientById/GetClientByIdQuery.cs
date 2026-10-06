using System;
using IdentityService.Application.Features.Clients.Dtos;
using MediatR;

namespace IdentityService.Application.Features.Clients.Queries.GetClientById;

public sealed record GetClientByIdQuery(Guid Id) : IRequest<ClientDetailDto>;

