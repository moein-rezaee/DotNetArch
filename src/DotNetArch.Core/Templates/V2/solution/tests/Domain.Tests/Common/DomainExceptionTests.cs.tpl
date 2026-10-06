using {{App}}.Domain.Common;

namespace {{App}}.Domain.Tests.Common;

public class DomainExceptionTests
{
    [Fact]
    public void RequireText_trims_valid_values() =>
        Assert.Equal("Widget", DomainException.RequireText("  Widget  ", "Name", 10));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RequireText_rejects_blank_values(string? value) =>
        Assert.Throws<DomainException>(() => DomainException.RequireText(value, "Name", 10));

    [Fact]
    public void RequireText_rejects_values_that_are_too_long() =>
        Assert.Throws<DomainException>(() => DomainException.RequireText(new string('x', 11), "Name", 10));
}
