using {{App}}.Domain.Common;

namespace {{App}}.Domain.Entities;

/// <summary>{{Entity}} aggregate. Behaviour lives here; state changes only through these methods.</summary>
public sealed partial class {{Entity}} : Entity
{
    public const int NameMaxLength = 200;

    private {{Entity}}()
    {
    }

    public string Name { get; private set; } = default!;

    public static {{Entity}} Create(string name, DateTime nowUtc)
    {
        var entity = new {{Entity}} { Name = DomainException.RequireText(name, nameof(Name), NameMaxLength) };
        entity.MarkCreated(nowUtc);
        return entity;
    }

    public void Rename(string name, DateTime nowUtc)
    {
        Name = DomainException.RequireText(name, nameof(Name), NameMaxLength);
        MarkUpdated(nowUtc);
    }
}
