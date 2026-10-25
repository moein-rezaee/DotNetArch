using DotNetArch.Core.Config;
using DotNetArch.Core.Hosting;
using DotNetArch.Core.Scaffolding.Entities;
using DotNetArch.Core.Scaffolding.Ops;
using DotNetArch.Core.Scaffolding.Solution;
using DotNetArch.Core.Scaffolding.V2;
using DotNetArch.Core.Tests.Support;
using Xunit;

namespace DotNetArch.Core.Tests;

/// <summary>Entities with typed properties and no CRUD, and features without an entity: the model and the home of typed use cases (R-J5, R-J6).</summary>
public sealed class EntityFeatureTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dotnet-arch-entity-" + Guid.NewGuid().ToString("N"));
    private readonly FakeProcessRunner _runner = new();

    public EntityFeatureTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private IDisposable Host() => ToolHost.Use(new HostContext(new NonInteractivePrompter(), new BufferedToolOutput(), _runner));

    private string P(string relative) => Path.Combine(_root, "Acme", relative);

    private SolutionConfig Solution(bool mcp = false)
    {
        using var _ = Host();
        SolutionGenerator.Generate(new SolutionRequest("Acme", _root, "Acme.Api", "controller", "SQLite",
            Ops: new OpsOptions(Ci: "none", NoGit: true, NoDocker: true, Mcp: mcp)));
        var config = ConfigManager.Load(P(""))!;
        EnumScaffolder.Generate(config, null, "BadgeStatus", new[] { "Active", "Revoked" });
        return config;
    }

    [Fact]
    public void Enum_members_follow_the_generated_zero_value()
    {
        Solution();

        var text = File.ReadAllText(P("src/Acme.Domain/Enums/BadgeStatus.cs"));
        Assert.Contains("None = 0,\n    Active = 1,\n    Revoked = 2", text);
        Assert.Throws<ArgumentException>(() => EntityGeneration.GenerateEnum(ConfigManager.Load(P(""))!, null, "Level", new[] { "A", "A" }));
    }

    [Fact]
    public void An_entity_gets_typed_state_mapping_dto_and_tests_without_crud()
    {
        var config = Solution();

        using (Host())
            EntityV2Generator.Generate(config, "Badge", EntityProperty.ParseList("Code:string(64)!,Owner:string(100)#,Status:BadgeStatus,Limit:int,Note:string(300)?,ExpiresAtUtc:DateTime?,Hash:string(128)!~"), "A badge.");

        var entity = File.ReadAllText(P("src/Acme.Domain/Entities/Badge.cs"));
        Assert.Contains("public const int CodeMaxLength = 64;", entity);
        Assert.Contains("public string? Note { get; private set; }", entity);
        Assert.Contains("Create(string code, string owner, BadgeStatus status, int limit, string hash, DateTime nowUtc, string? note = default, DateTime? expiresAtUtc = default)", entity);
        Assert.Contains("Code = DomainException.RequireText(code, nameof(Code), CodeMaxLength),", entity);
        Assert.Contains("Note = DomainException.OptionalText(note, nameof(Note), NoteMaxLength),", entity);

        var mapping = File.ReadAllText(P("src/Acme.Infrastructure/Persistence/Configurations/BadgeConfiguration.cs"));
        Assert.Contains("builder.HasIndex(entity => entity.Code).IsUnique();", mapping);
        Assert.Contains("builder.HasIndex(entity => entity.Owner);", mapping);
        Assert.Contains("entity.Status).HasConversion<string>()", mapping);
        Assert.Contains("BadgeDto(Guid Id, string Code, string Owner, BadgeStatus Status, int Limit, string? Note, DateTime? ExpiresAtUtc, DateTime CreatedAtUtc, DateTime? UpdatedAtUtc)", File.ReadAllText(P("src/Acme.Application/Features/Badges/Dtos/BadgeDto.cs")));
        Assert.DoesNotContain("Hash", File.ReadAllText(P("src/Acme.Application/Features/Badges/Dtos/BadgeDto.cs")));
        Assert.Contains("public string Hash { get; private set; }", entity);
        Assert.Contains("Create_rejects_a_blank_code", File.ReadAllText(P("tests/Acme.Domain.Tests/Entities/BadgeTests.cs")));
        Assert.Contains("Code_is_unique", File.ReadAllText(P("tests/Acme.Infrastructure.Tests/Persistence/BadgePersistenceTests.cs")));
        Assert.False(Directory.Exists(P("src/Acme.Application/Features/Badges/Commands")));
        Assert.True(ConfigManager.Load(P(""))!.Entities["Badge"].HasModel);
    }

    [Theory]
    [InlineData("code")]
    [InlineData("Code:Dictionary<string,string>")]
    [InlineData("Id:Guid")]
    [InlineData("Count:int(5)")]
    [InlineData("Code:string,Code:string")]
    public void Unsupported_properties_are_rejected(string properties) =>
        Assert.Throws<ArgumentException>(() => EntityProperty.ParseList(properties));

    [Fact]
    public void An_unknown_enum_is_rejected_before_anything_is_written()
    {
        var config = Solution();

        using var _ = Host();
        Assert.Throws<ArgumentException>(() => EntityV2Generator.Generate(config, "Badge", EntityProperty.ParseList("Kind:MissingEnum"), null));
        Assert.False(File.Exists(P("src/Acme.Domain/Entities/Badge.cs")));
    }

    [Fact]
    public void A_use_case_that_names_an_enum_gets_the_enum_namespace()
    {
        var config = Solution();

        using (Host())
        {
            FeatureV2Generator.Generate(config, "Setting", null);
            ActionScaffolder.Generate(config, "Setting", "Find", "GET", crudStyle: false, "Status:BadgeStatus?", "Names:List<string>", false, null);
        }

        Assert.Contains("using Acme.Domain.Enums;", File.ReadAllText(P("src/Acme.Application/Features/Settings/Actions/FindSetting/FindSettingQuery.cs")));
        Assert.DoesNotContain("Features.Settings.Dtos", File.ReadAllText(P("src/Acme.Application/Features/Settings/Actions/FindSetting/FindSettingQuery.cs")));
    }

    [Fact]
    public void A_feature_holds_typed_use_cases_that_need_an_explicit_output()
    {
        var config = Solution(mcp: true);

        using (Host())
        {
            FeatureV2Generator.Generate(config, "Setting", "Tenant settings.");
            Assert.Throws<ArgumentException>(() => ActionScaffolder.Generate(config, "Setting", "Set", "PUT", crudStyle: false, "Key:string", null, false, null));
            ActionScaffolder.Generate(config, "Setting", "Set", "PUT", crudStyle: false, "Key:string,Value:string", "none", false, null);
        }

        Assert.Contains("partial class SettingsController(ISender sender)", File.ReadAllText(P("src/Acme.Api/Controllers/Settings/SettingsController.cs")));
        Assert.Contains("[HttpPut(\"set\")]", File.ReadAllText(P("src/Acme.Api/Controllers/Settings/SettingsController.Set.cs")));
        Assert.Contains("class SettingsTools(ISender sender)", File.ReadAllText(P("src/Acme.Mcp/Tools/Settings/SettingsTools.cs")));
        Assert.True(File.Exists(P("src/Acme.Mcp/Tools/Settings/SettingsTools.Set.cs")));
        Assert.True(ConfigManager.Load(P(""))!.Entities["Setting"].IsFeature);
        Assert.Contains("entity.Setting: feature", File.ReadAllText(P("dotnet-arch.yml")));
    }
}
