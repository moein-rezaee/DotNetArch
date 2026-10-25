using {{App}}.Domain.Common;
{{EnumUsing}}
namespace {{App}}.Domain.Entities;

/// <summary>{{Summary}} Behaviour lives here; state changes only through these methods.</summary>
public sealed partial class {{Entity}} : Entity
{
{{Constants}}

    private {{Entity}}()
    {
    }

{{Properties}}

    public static {{Entity}} Create({{CreateParameters}})
    {
        var entity = new {{Entity}}
        {
{{Assignments}}
        };
        entity.MarkCreated(nowUtc);
        return entity;
    }
}
