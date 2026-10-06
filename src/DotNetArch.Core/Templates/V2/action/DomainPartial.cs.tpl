using {{App}}.Domain.Common;

namespace {{App}}.Domain.Entities;

public sealed partial class {{Entity}}
{
    /// <summary>Business operation '{{ActionName}}'. TODO: enforce the rules and change state here, never in the handler.</summary>
    public void {{ActionName}}(DateTime nowUtc)
    {
        MarkUpdated(nowUtc);
    }
}
