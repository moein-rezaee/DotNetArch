using {{App}}.Domain.Common;
using {{App}}.Domain.Entities;

namespace {{App}}.Domain.Tests.Entities;

public class {{Entity}}Tests
{
    private static readonly DateTime Now = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_sets_state_and_audit_timestamp()
    {
        var entity = {{Entity}}.Create("  Widget  ", Now);

        Assert.NotEqual(Guid.Empty, entity.Id);
        Assert.Equal("Widget", entity.Name);
        Assert.Equal(Now, entity.CreatedAtUtc);
        Assert.Null(entity.UpdatedAtUtc);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_blank_names(string name) =>
        Assert.Throws<DomainException>(() => {{Entity}}.Create(name, Now));

    [Fact]
    public void Create_rejects_names_longer_than_the_limit() =>
        Assert.Throws<DomainException>(() => {{Entity}}.Create(new string('x', {{Entity}}.NameMaxLength + 1), Now));

    [Fact]
    public void Rename_changes_the_name_and_marks_the_entity_updated()
    {
        var entity = {{Entity}}.Create("Old", Now);
        var later = Now.AddHours(1);

        entity.Rename("New", later);

        Assert.Equal("New", entity.Name);
        Assert.Equal(later, entity.UpdatedAtUtc);
    }
}
