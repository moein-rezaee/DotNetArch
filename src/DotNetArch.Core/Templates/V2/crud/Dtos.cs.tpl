using {{App}}.Domain.Entities;

namespace {{App}}.Application.Features.{{Plural}}.Dtos;

public sealed record {{Entity}}Dto(Guid Id, string Name, DateTime CreatedAtUtc, DateTime? UpdatedAtUtc);

public sealed record Create{{Entity}}Request(string Name);

public sealed record Update{{Entity}}Request(string Name);

public static class {{Entity}}Mappings
{
    public static {{Entity}}Dto ToDto(this {{Entity}} entity) =>
        new(entity.Id, entity.Name, entity.CreatedAtUtc, entity.UpdatedAtUtc);
}
