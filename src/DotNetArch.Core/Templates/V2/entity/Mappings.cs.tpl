using {{App}}.Application.Features.{{Plural}}.Dtos;
using {{App}}.Domain.Entities;

namespace {{App}}.Application.Features.{{Plural}}.Mappings;

/// <summary>Entity to DTO mapping. It lives apart from the DTO record so the DTO (pure data) can sit in Application.Contracts while the mapping, which knows the entity, stays in Application.</summary>
public static class {{Entity}}Mappings
{
    public static {{Entity}}Dto ToDto(this {{Entity}} entity) =>
        new({{DtoArguments}});
}
