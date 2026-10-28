namespace Corevia.Kit.{{Name}}.Tests;

public class SmokeTests
{
    [Fact]
    public void The_abstractions_assembly_carries_the_package_name()
    {
        Assert.Equal("Corevia.Kit.{{Name}}.Abstractions", typeof({{Name}}AssemblyMarker).Assembly.GetName().Name);
    }
}
