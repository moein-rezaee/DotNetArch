using DotNetArch.Core.Config;
using DotNetArch.Core.Hosting;
using DotNetArch.Core.Operations;
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
    public void A_use_case_of_a_feature_can_return_the_DTO_of_another_entity()
    {
        var config = Solution();

        using (Host())
        {
            EntityV2Generator.Generate(config, "Badge", EntityProperty.ParseList("Code:string(64)!,Status:BadgeStatus"), null);
            FeatureV2Generator.Generate(config, "MyBadge", null);
            ActionScaffolder.Generate(config, "MyBadge", "Search", "GET", crudStyle: false, "Query:string?", "Items:List<BadgeDto>", false, null);
        }

        var query = File.ReadAllText(P("src/Acme.Application/Features/MyBadges/Actions/SearchMyBadge/SearchMyBadgeResult.cs"));
        Assert.Contains("using Acme.Application.Features.Badges.Dtos;", query);
        Assert.DoesNotContain("Features.MyBadges.Dtos", query);
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

    [Fact]
    public void A_controller_that_already_lives_in_the_HttpApi_layer_is_not_declared_again_in_the_Api_project()
    {
        var config = Solution();
        Directory.CreateDirectory(P("src/Acme.HttpApi/Controllers/Settings"));
        File.WriteAllText(P("src/Acme.HttpApi/Controllers/Settings/SettingsController.cs"), "// moved here by the layer migration");

        using (Host())
            FeatureV2Generator.EnsureSkeleton(config, "Setting");

        Assert.False(File.Exists(P("src/Acme.Api/Controllers/Settings/SettingsController.cs")));
    }

    [Fact]
    public void The_length_of_a_typed_input_is_recorded_so_that_regenerating_keeps_it()
    {
        var config = Solution();

        using (Host())
        {
            FeatureV2Generator.Generate(config, "Setting", null);
            ActionScaffolder.Generate(config, "Setting", "Set", "PUT", crudStyle: false, "Key:string(64),Value:string(8000),Note:string(200)?,Count:int", "none", false, null);
        }

        Assert.Contains("usecase.Setting.Set: PUT|protected|Key:string(64),Value:string(8000),Note:string(200)?,Count:int|none", File.ReadAllText(P("dotnet-arch.yml")));
    }

    [Fact]
    public void A_typed_input_can_set_the_length_its_string_is_validated_against()
    {
        var spec = UseCaseSpec.Parse("Code:string,Document:string(8000),Note:string(200)?", "none");

        Assert.Equal(new int?[] { null, 8000, 200 }, spec.Input.Select(p => p.MaxLength));
        Assert.Equal("string?", spec.Input[2].Type);
        var rules = spec.ValidatorRules();
        Assert.Contains("command.Code).NotEmpty().MaximumLength(512)", rules);
        Assert.Contains("command.Document).NotEmpty().MaximumLength(8000)", rules);
        Assert.DoesNotContain("Note", rules);
        Assert.Throws<ArgumentException>(() => UseCaseSpec.Parse("Code:string(0)", "none"));
    }

    [Fact]
    public void The_repository_port_pages_newest_first_and_counts()
    {
        Solution();

        var port = File.ReadAllText(P("src/Acme.Application/Abstractions/Persistence/IRepository.cs"));
        Assert.Contains("bool newestFirst = false", port);
        Assert.Contains("Task<int> CountAsync(", port);
        Assert.Contains("OrderByDescending(entity => entity.CreatedAtUtc)", File.ReadAllText(P("src/Acme.Infrastructure/Persistence/Repositories/EfRepository.cs")));
    }

    [Fact]
    public void A_blueprint_generates_what_it_describes_in_one_plan_and_skips_what_exists()
    {
        Solution();
        var file = Path.Combine(_root, "blueprint.yaml");
        File.WriteAllText(file, """
            schema: 1
            version: 1.0.0
            enums:
              - name: Level
                members: [Low, High]
            entities:
              - name: Badge
                description: A badge.
                properties: "Code:string(64)!,Level:Level"
            features:
              - name: MyBadge
            use_cases:
              - entity: Badge
                action: Find
                method: GET
                input: "Id:Guid"
                output: "Code:string"
                description: Find a badge
              - entity: MyBadge
                action: Search
                method: GET
                input: "Query:string?"
                output: "Items:List<BadgeDto>"
            services:
              - name: BadgeNumberService
                entity: Badge
            """);

        using var _ = Host();
        OperationResult Run(bool apply) => OperationRegistry.Execute(OperationRegistry.Find("apply_blueprint")!, new Dictionary<string, string> { ["path"] = P(""), ["blueprint"] = file }, apply);

        var plan = Run(apply: false);
        Assert.True(plan.Ok, plan.Error);
        Assert.False(File.Exists(P("src/Acme.Domain/Entities/Badge.cs")));
        Assert.Contains(plan.Plan!, change => change.Path.EndsWith("Badge.cs", StringComparison.Ordinal));

        var applied = Run(apply: true);
        Assert.True(applied.Ok, applied.Error);
        Assert.True(File.Exists(P("src/Acme.Domain/Entities/Badge.cs")));
        Assert.True(File.Exists(P("src/Acme.Domain/Enums/Level.cs")));
        Assert.Contains("using Acme.Application.Features.Badges.Dtos;", File.ReadAllText(P("src/Acme.Application/Features/MyBadges/Actions/SearchMyBadge/SearchMyBadgeResult.cs")));
        Assert.True(File.Exists(P("src/Acme.Application/Features/Badges/Services/BadgeNumberService.cs")));
        var recorded = File.ReadAllText(P("dotnet-arch.yml"));
        Assert.Contains("usecase.Badge.Find:", recorded);
        Assert.Contains("usecase.MyBadge.Search:", recorded);

        var again = Run(apply: true);
        Assert.True(again.Ok, again.Error);
        Assert.Empty(again.Plan!);
        Assert.Equal(recorded, File.ReadAllText(P("dotnet-arch.yml")));
    }

    [Fact]
    public void A_blueprint_with_a_missing_name_is_refused_before_anything_is_written()
    {
        Solution();
        var file = Path.Combine(_root, "blueprint.yaml");
        File.WriteAllText(file, "schema: 1\nversion: 1.0.0\nentities:\n  - description: nameless\n");

        using var _ = Host();
        var result = OperationRegistry.Execute(OperationRegistry.Find("apply_blueprint")!, new Dictionary<string, string> { ["path"] = P(""), ["blueprint"] = file }, apply: true);

        Assert.False(result.Ok);
        Assert.Contains("entity name", result.Error);
    }

    private OperationResult ApplyBlueprint(string file, bool apply = true) =>
        OperationRegistry.Execute(OperationRegistry.Find("apply_blueprint")!, new Dictionary<string, string> { ["path"] = P(""), ["blueprint"] = file }, apply);

    private string WriteBlueprint(string body, string version = "1.0.0", string schema = "1")
    {
        var file = Path.Combine(P("docs"), "specs", "blueprint.yaml");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, (schema.Length == 0 ? string.Empty : $"schema: {schema}\n") + (version.Length == 0 ? string.Empty : $"version: {version}\n") + body);
        return file;
    }

    private const string BadgeBody = "enums:\n  - { name: Level, members: [Low, High] }\nentities:\n  - { name: Badge, properties: \"Code:string(64)!,Level:Level\" }\n";

    [Theory]
    [InlineData("1.0.0", "", "needs 'schema: 1'")]
    [InlineData("", "1", "needs 'version")]
    [InlineData("1.0", "1", "needs 'version")]
    [InlineData("1.0.0", "2", "Upgrade the tool")]
    public void A_blueprint_without_a_known_schema_and_a_semver_version_is_refused(string version, string schema, string expected)
    {
        Solution();
        var file = WriteBlueprint(BadgeBody, version, schema);

        using var _ = Host();
        var result = ApplyBlueprint(file);

        Assert.False(result.Ok);
        Assert.Contains(expected, result.Error);
        Assert.False(File.Exists(P("src/Acme.Domain/Entities/Badge.cs")));
    }

    [Fact]
    public void A_blueprint_only_moves_forward_and_every_change_of_the_model_needs_a_new_version()
    {
        Solution();
        using var _ = Host();
        var file = WriteBlueprint(BadgeBody);
        Assert.True(ApplyBlueprint(file).Ok);
        Assert.Contains("version: 1.0.0", File.ReadAllText(P(".net-arch/service-blueprint.lock")));

        WriteBlueprint(BadgeBody + "features:\n  - { name: MyBadge }\n");
        var unchanged = ApplyBlueprint(file);
        Assert.False(unchanged.Ok);
        Assert.Contains("raise 'version'", unchanged.Error);

        WriteBlueprint(BadgeBody + "features:\n  - { name: MyBadge }\n", "0.9.0");
        Assert.Contains("lower than the applied", ApplyBlueprint(file).Error);

        WriteBlueprint(BadgeBody + "features:\n  - { name: MyBadge }\n", "1.1.0");
        Assert.True(ApplyBlueprint(file).Ok);
        Assert.Contains("version: 1.1.0", File.ReadAllText(P(".net-arch/service-blueprint.lock")));
        Assert.True(ConfigManager.Load(P(""))!.Entities["MyBadge"].IsFeature);
    }

    [Fact]
    public void The_doctor_checks_the_blueprint_and_fix_applies_it()
    {
        Solution();
        using var _ = Host();
        var ids = () => DotNetArch.Core.Doctor.DoctorRunner.Run(P("")).Findings.Where(f => f.Id.StartsWith("DA-BP", StringComparison.Ordinal)).Select(f => f.Id).ToList();

        Assert.Equal(new[] { "DA-BP01" }, ids());

        var file = WriteBlueprint(BadgeBody);
        Assert.Equal(new[] { "DA-BP03", "DA-BP05" }, ids().OrderBy(id => id, StringComparer.Ordinal));

        Assert.True(ApplyBlueprint(file).Ok);
        Assert.Empty(ids());

        WriteBlueprint(BadgeBody + "features:\n  - { name: MyBadge }\n");
        Assert.Equal(new[] { "DA-BP03", "DA-BP05" }, ids().OrderBy(id => id, StringComparer.Ordinal));

        WriteBlueprint(BadgeBody + "features:\n  - { name: MyBadge }\n", "1.1.0");
        Assert.Equal(new[] { "DA-BP03", "DA-BP05" }, ids().OrderBy(id => id, StringComparer.Ordinal));

        var plan = OperationRegistry.Execute(OperationRegistry.Find("fix")!, new Dictionary<string, string> { ["path"] = P(""), ["rules"] = "DA-BP03,DA-BP05" }, apply: false);
        Assert.True(plan.Ok);
        Assert.Contains(plan.Plan!, change => change.RuleId == "DA-BP03");
        Assert.False(ConfigManager.Load(P(""))!.Entities.ContainsKey("MyBadge"));

        var fixedUp = OperationRegistry.Execute(OperationRegistry.Find("fix")!, new Dictionary<string, string> { ["path"] = P(""), ["rules"] = "DA-BP03,DA-BP05" }, apply: true);
        Assert.True(fixedUp.Ok);
        Assert.Empty(ids());

        File.AppendAllText(P("dotnet-arch.yml"), "entity.Stray: model\n");
        Assert.Equal(new[] { "DA-BP04" }, ids());
    }

    [Fact]
    public void The_typed_client_expresses_a_GET_that_binds_a_query_record()
    {
        Solution();
        using var _ = Host();
        var file = WriteBlueprint("""
            enums:
              - { name: Level, members: [Low, High] }
            entities:
              - { name: Badge, properties: "Code:string(64)!,Level:Level" }
            use_cases:
              - { entity: Badge, action: Find, method: GET, input: "Id:Guid,Level:Level?,Names:List<string>", output: "Code:string" }
            layers: [Domain.Shared, Application.Contracts, HttpApi]
            """);
        Assert.True(ApplyBlueprint(file).Ok);

        var plan = OperationRegistry.Execute(OperationRegistry.Find("fix")!, new Dictionary<string, string> { ["path"] = P(""), ["rules"] = "DA-A08" }, apply: true);

        Assert.True(plan.Ok, plan.Error);
        var data = System.Text.Json.JsonSerializer.Serialize(plan.Data);
        Assert.DoesNotContain("skipped", data);
        var client = Directory.EnumerateFiles(P("src"), "BadgesClient.cs", SearchOption.AllDirectories).Single();
        Assert.Contains("FindAsync(Guid id, List<string> names, Level? level", File.ReadAllText(client));
        Assert.Contains("(\"id\", id), (\"names\", names), (\"level\", level)", File.ReadAllText(client));
    }

    [Fact]
    public void An_env_file_that_gitignore_covers_is_not_a_finding()
    {
        Solution();
        File.WriteAllText(P("src/Acme.Api/.env"), "DATABASE_CONNECTION_STRING=x\n");

        Assert.DoesNotContain(DotNetArch.Core.Doctor.DoctorRunner.Run(P("")).Findings, f => f.Id == "DA-C04");

        File.WriteAllText(P(".gitignore"), "bin/\nobj/\n");
        Assert.Contains(DotNetArch.Core.Doctor.DoctorRunner.Run(P("")).Findings, f => f.Id == "DA-C04");
    }

    [Fact]
    public void Applying_a_blueprint_brings_the_recorded_string_lengths_in_line()
    {
        Solution();
        using var _ = Host();
        var file = WriteBlueprint("entities:\n  - { name: Badge, properties: \"Code:string(64)!\" }\nuse_cases:\n  - { entity: Badge, action: Find, method: GET, input: \"Id:Guid,Text:string(8000)\", output: \"Code:string\" }\n");
        Assert.True(ApplyBlueprint(file).Ok);
        var path = P("dotnet-arch.yml");
        File.WriteAllText(path, File.ReadAllText(path).Replace("Text:string(8000)", "Text:string"));
        Assert.Contains("Id:Guid,Text:string|", File.ReadAllText(path));

        WriteBlueprint("entities:\n  - { name: Badge, properties: \"Code:string(64)!\" }\nuse_cases:\n  - { entity: Badge, action: Find, method: GET, input: \"Id:Guid,Text:string(8000)\", output: \"Code:string\" }\n", "1.0.1");
        Assert.True(ApplyBlueprint(file).Ok);

        Assert.Contains("Id:Guid,Text:string(8000)|", File.ReadAllText(path));
    }

    [Fact]
    public void Fix_derives_a_blueprint_for_a_service_that_has_none_and_applies_it()
    {
        Solution();
        using var _ = Host();
        const string body = "enums:\n  - { name: Level, members: [Low, High] }\nentities:\n  - { name: Badge, description: \"A badge.\", properties: \"Code:string(64)!,Owner:string(100)#,Level:Level,Note:string(300)?,Hash:string(128)!~,Expires:DateTime?\" }\nfeatures:\n  - { name: MyBadge }\nuse_cases:\n  - { entity: Badge, action: Find, method: GET, input: \"Id:Guid\", output: \"Code:string\" }\n  - { entity: MyBadge, action: Search, method: GET, input: \"Query:string(50)?\", output: \"Items:List<BadgeDto>\" }\nservices:\n  - { name: BadgeNumberService, entity: Badge }\n";
        var original = WriteBlueprint(body);
        Assert.True(ApplyBlueprint(original).Ok);
        File.Delete(original);
        File.Delete(P(".net-arch/service-blueprint.lock"));
        Assert.Contains("DA-BP01", DotNetArch.Core.Doctor.DoctorRunner.Run(P("")).Findings.Select(f => f.Id));

        var fixedUp = OperationRegistry.Execute(OperationRegistry.Find("fix")!, new Dictionary<string, string> { ["path"] = P(""), ["rules"] = "DA-BP01" }, apply: true);

        Assert.True(fixedUp.Ok, fixedUp.Error);
        var derived = File.ReadAllText(original);
        Assert.Contains("schema: 1", derived);
        Assert.Contains("version: 1.0.0", derived);
        Assert.Contains("{ name: Level, members: [Low, High] }", derived);
        Assert.Contains("description: \"A badge.\"", derived);
        Assert.Contains("properties: \"Code:string(64)!,Owner:string(100)#,Level:Level,Note:string(300)?,Hash:string(128)!~,Expires:DateTime?\"", derived);
        Assert.Contains("{ name: MyBadge }", derived);
        Assert.Contains("{ name: BadgeNumberService, entity: Badge }", derived);
        Assert.Contains("input: \"Query:string(50)?\"", derived);
        Assert.DoesNotContain(DotNetArch.Core.Doctor.DoctorRunner.Run(P("")).Findings, f => f.Id.StartsWith("DA-BP", StringComparison.Ordinal));
    }
}
