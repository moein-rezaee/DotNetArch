using {{App}}.Domain.Common;
using {{App}}.Domain.Entities;
{{EnumUsing}}
namespace {{App}}.Domain.Tests.Entities;

public class {{Entity}}Tests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_sets_state_and_audit_timestamp()
    {
        var entity = {{Entity}}.Create({{SampleArguments}});

        Assert.NotEqual(Guid.Empty, entity.Id);
{{StateAssertions}}
        Assert.Equal(Now, entity.CreatedAtUtc);
        Assert.Null(entity.UpdatedAtUtc);
    }
{{RequiredTextTests}}}
