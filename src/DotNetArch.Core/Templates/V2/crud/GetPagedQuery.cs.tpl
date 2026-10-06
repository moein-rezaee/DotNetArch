using {{App}}.Application.Common.Pagination;
using {{App}}.Application.Features.{{Plural}}.Dtos;
using MediatR;

namespace {{App}}.Application.Features.{{Plural}}.Queries.Get{{Plural}};

public sealed record Get{{Plural}}Query(int PageNumber = 1, int PageSize = 20) : IRequest<PagedResult<{{Entity}}Dto>>;
