using {{App}}.Domain.Entities;

namespace {{App}}.Application.Features.{{Plural}}.Dtos;

/// <summary>Entity to DTO mapping. It lives apart from the DTO records so the DTOs (pure data) can sit in Application.Contracts while the mapping, which knows the entity, stays in Application.</summary>
public static class {{Entity}}Mappings
{
    public static {{Entity}}Dto ToDto(this {{Entity}} entity) =>
        new(entity.Id, entity.Name, entity.CreatedAtUtc, entity.UpdatedAtUtc);
}
