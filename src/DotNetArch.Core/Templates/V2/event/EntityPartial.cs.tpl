using {{App}}.Domain.Common;
using {{App}}.Domain.Events.{{Plural}};

namespace {{App}}.Domain.Entities;

public sealed partial class {{Entity}}
{
    /// <summary>Records that '{{EventName}}' happened; call it from the behaviour that causes it. Dispatched after the unit of work commits.</summary>
    public void Raise{{EventName}}(DateTime nowUtc) => Raise(new {{Entity}}{{EventName}}Event(Id, nowUtc));
}
