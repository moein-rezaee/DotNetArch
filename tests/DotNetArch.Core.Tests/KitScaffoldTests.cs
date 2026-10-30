using DotNetArch.Core.Hosting;
using DotNetArch.Core.Operations;
using DotNetArch.Core.Tests.Support;
using Xunit;

namespace DotNetArch.Core.Tests;

/// <summary>The tool creates Kit areas and their provider packages (R-J20).</summary>
public sealed class KitScaffoldTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dotnet-arch-kit-" + Guid.NewGuid().ToString("N"));
    private readonly FakeProcessRunner _runner = new();

    public KitScaffoldTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "packages"));
        File.WriteAllText(Path.Combine(_root, "Corevia.Kit.packages.proj"),
            "<Project>\n  <ItemGroup>\n    <ProjectFiles Include=\"$(MSBuildThisFileDirectory)packages/A/A.csproj\" />\n  </ItemGroup>\n  <Target Name=\"Restore\">\n  </Target>\n</Project>\n");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private OperationResult Run(string name, bool apply, params (string Key, string Value)[] values)
    {
        using var _ = ToolHost.Use(new HostContext(new NonInteractivePrompter(), new BufferedToolOutput(), _runner));
        var map = new Dictionary<string, string> { ["path"] = _root };
        foreach (var (key, value) in values)
            map[key] = value;
        return OperationRegistry.Execute(OperationRegistry.Find(name)!, map, apply);
    }

    private string Read(string relative) => File.ReadAllText(Path.Combine(_root, relative));

    [Fact]
    public void A_plan_writes_nothing()
    {
        var result = Run("scaffold_kit", false, ("name", "Delivery"), ("description", "Deliver a message."));

        Assert.True(result.Ok, result.Error);
        Assert.Contains(result.Plan!, p => p.Path.EndsWith("Corevia.Kit.Delivery.Core.csproj", StringComparison.Ordinal));
        Assert.False(Directory.Exists(Path.Combine(_root, "packages", "Delivery")));
    }

    [Fact]
    public void A_kit_gets_packages_tests_bilingual_docs_and_the_package_list_entry()
    {
        var result = Run("scaffold_kit", true, ("name", "Delivery"), ("description", "Deliver a message."), ("description_fa", "رساندن پیام."));

        Assert.True(result.Ok, result.Error);
        foreach (var file in new[]
                 {
                     "packages/Delivery/Corevia.Kit.Delivery.Abstractions/Corevia.Kit.Delivery.Abstractions.csproj",
                     "packages/Delivery/Corevia.Kit.Delivery.Core/DependencyInjection.cs",
                     "packages/Delivery/README.md", "packages/Delivery/README.fa.md", "packages/Delivery/AGENTS.md",
                     "packages/Delivery/docs/specs/contracts.md", "packages/Delivery/docs/specs/contracts.fa.md",
                     "tests/Delivery/Corevia.Kit.Delivery.Tests/SmokeTests.cs",
                 })
            Assert.True(File.Exists(Path.Combine(_root, file)), file);

        Assert.Contains("رساندن پیام", Read("packages/Delivery/README.fa.md"));
        var list = Read("Corevia.Kit.packages.proj");
        Assert.Contains("packages/Delivery/Corevia.Kit.Delivery.Abstractions/Corevia.Kit.Delivery.Abstractions.csproj", list);
        Assert.Contains("packages/Delivery/Corevia.Kit.Delivery.Core/Corevia.Kit.Delivery.Core.csproj", list);
        Assert.True(list.IndexOf("Delivery.Core.csproj", StringComparison.Ordinal) < list.IndexOf("<Target", StringComparison.Ordinal));
        Assert.Contains("Delivery.Core.csproj", Read("tests/Delivery/Corevia.Kit.Delivery.Tests/Corevia.Kit.Delivery.Tests.csproj"));
    }

    [Fact]
    public void A_kit_that_exists_is_refused_and_a_kit_can_be_only_contracts()
    {
        Run("scaffold_kit", true, ("name", "Chat"), ("description", "Bots."), ("abstractions_only", "true"));
        Assert.False(Directory.Exists(Path.Combine(_root, "packages", "Chat", "Corevia.Kit.Chat.Core")));

        var again = Run("scaffold_kit", false, ("name", "Chat"), ("description", "Bots."));
        Assert.False(again.Ok);
        Assert.Contains("already exists", again.Error);
    }

    [Fact]
    public void A_provider_is_added_to_the_project_list_the_tests_and_the_docs()
    {
        Run("scaffold_kit", true, ("name", "Delivery"), ("description", "Deliver a message."));

        var result = Run("scaffold_provider", true, ("kit", "Delivery"), ("provider", "Sms.Kavenegar"), ("description", "SMS through Kavenegar."));

        Assert.True(result.Ok, result.Error);
        var di = Read("packages/Delivery/Corevia.Kit.Delivery.Providers.Sms.Kavenegar/DependencyInjection.cs");
        Assert.Contains("AddCoreviaDeliverySmsKavenegar", di);
        Assert.Contains("namespace Corevia.Kit.Delivery.Providers.Sms.Kavenegar;", di);
        Assert.Contains("Providers.Sms.Kavenegar.csproj", Read("Corevia.Kit.packages.proj"));
        Assert.Contains("Providers.Sms.Kavenegar.csproj", Read("tests/Delivery/Corevia.Kit.Delivery.Tests/Corevia.Kit.Delivery.Tests.csproj"));
        Assert.Contains("`Corevia.Kit.Delivery.Providers.Sms.Kavenegar`: SMS through Kavenegar", Read("packages/Delivery/README.md"));
        Assert.Contains("Kavenegar", Read("packages/Delivery/docs/specs/contracts.fa.md"));
        Assert.False(Run("scaffold_provider", false, ("kit", "Delivery"), ("provider", "Sms.Kavenegar"), ("description", "x")).Ok);
    }

    [Fact]
    public void An_adapter_is_the_same_as_a_provider_with_the_vocabulary_of_the_composition_model()
    {
        Run("scaffold_kit", true, ("name", "Delivery"), ("description", "Deliver a message."));

        var result = Run("scaffold_adapter", true, ("kit", "Delivery"), ("adapter", "Sms.Kavenegar"), ("description", "SMS through Kavenegar."));

        Assert.True(result.Ok, result.Error);
        Assert.Contains("namespace Corevia.Kit.Delivery.Adapters.Sms.Kavenegar;", Read("packages/Delivery/Corevia.Kit.Delivery.Adapters.Sms.Kavenegar/DependencyInjection.cs"));
        Assert.Contains("<PackageId>Corevia.Kit.Delivery.Adapters.Sms.Kavenegar</PackageId>", Read("packages/Delivery/Corevia.Kit.Delivery.Adapters.Sms.Kavenegar/Corevia.Kit.Delivery.Adapters.Sms.Kavenegar.csproj"));
        Assert.Contains("Adapters.Sms.Kavenegar.csproj", Read("Corevia.Kit.packages.proj"));
        Assert.False(Run("scaffold_adapter", false, ("kit", "Delivery"), ("adapter", "Sms.Kavenegar"), ("description", "x")).Ok);
    }

    [Theory]
    [InlineData("delivery")]
    [InlineData("De-livery")]
    public void Names_must_be_pascal_case(string name)
    {
        var result = Run("scaffold_kit", false, ("name", name), ("description", "x"));

        Assert.False(result.Ok);
    }

    [Fact]
    public void A_provider_needs_an_existing_kit_and_a_kit_repository()
    {
        Assert.False(Run("scaffold_provider", false, ("kit", "Missing"), ("provider", "Sms"), ("description", "x")).Ok);
        Directory.Delete(Path.Combine(_root, "packages"));

        Assert.False(Run("scaffold_kit", false, ("name", "Delivery"), ("description", "x")).Ok);
    }
}
