namespace IdentityService.Application.Common.Pagination;

public sealed record PagedResponse<T>(IReadOnlyCollection<T> Items, int PageNumber, int PageSize, int TotalCount);

