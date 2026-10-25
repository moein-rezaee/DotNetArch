using System.Text;
using DotNetArch.Core.Templating;

namespace DotNetArch.Core.Scaffolding.V2;

/// <summary>
/// <c>new entity</c>: a domain entity with typed properties and its persistence, without CRUD use cases. It is the model for typed use cases
/// (<c>new action --input --output</c>): the aggregate, its EF mapping (lengths, enum conversion, indexes), the DTO and mapping, a domain test and a
/// persistence test. Behaviour is added by hand in a partial class or by use cases.
/// </summary>
internal static class EntityV2Generator
{
    public static bool Generate(SolutionConfig config, string entity, IReadOnlyList<EntityProperty> properties, string? description)
    {
        Identifier.RequireIdentifier(entity, "entity name");
        var names = V2Names.For(config, entity);
        if (EntityGeneration.EntityExists(config, entity))
        {
            ToolHost.Error($"Entity '{entity}' already exists.", "Add behaviour in a partial class or with 'new action'.");
            return false;
        }

        foreach (var property in properties.Where(p => p.IsEnum))
        {
            var enumFile = Directory.Exists(Path.Combine(config.SolutionPath, "src"))
                ? Directory.EnumerateFiles(Path.Combine(config.SolutionPath, "src"), $"{property.Type}.cs", SearchOption.AllDirectories).Any(f => f.Replace('\\', '/').Contains("/Enums/", StringComparison.Ordinal))
                : false;
            if (!enumFile)
                throw new ArgumentException($"Enum '{property.Type}' (property {property.Name}) does not exist. Create it first with 'new enum --enum={property.Type}'.");
        }

        var tokens = Tokens(config, names, entity, properties, description);
        var writer = new FileWriter(config.SolutionPath);
        writer.Write($"{names.DomainProject}/Entities/{entity}.cs", TemplateRenderer.RenderTemplate("V2/entity/DomainEntity.cs.tpl", tokens));
        writer.Write($"{names.InfrastructureProject}/Persistence/Configurations/{entity}Configuration.cs", TemplateRenderer.RenderTemplate("V2/entity/EfConfiguration.cs.tpl", tokens));
        writer.Write($"{names.FeatureFolder}/Dtos/{entity}Dto.cs", TemplateRenderer.RenderTemplate("V2/entity/Dto.cs.tpl", tokens));
        writer.Write($"{names.FeatureFolder}/Mappings/{entity}Mappings.cs", TemplateRenderer.RenderTemplate("V2/entity/Mappings.cs.tpl", tokens));

        if (Directory.Exists(Path.Combine(config.SolutionPath, config.TestsRoot, $"{config.SolutionName}.Domain.Tests")))
        {
            writer.Write($"{config.TestsRoot}/{config.SolutionName}.Domain.Tests/Entities/{entity}Tests.cs", TemplateRenderer.RenderTemplate("V2/entity-tests/DomainTests.cs.tpl", tokens));
            writer.Write($"{config.TestsRoot}/{config.SolutionName}.Infrastructure.Tests/Persistence/{entity}PersistenceTests.cs", TemplateRenderer.RenderTemplate("V2/entity-tests/InfrastructureTests.cs.tpl", tokens));
        }

        MigrationService.AddMigration(config, $"Auto_{entity}");

        if (!config.Entities.TryGetValue(entity, out var state))
            state = new EntityStatus();
        state.HasModel = !state.HasCrud && !state.HasAction;
        config.Entities[entity] = state;
        ConfigManager.Save(config.SolutionPath, config);

        ToolHost.Success($"Entity {entity} generated ({writer.Created.Count} files, {properties.Count} properties).");
        return true;
    }

    private static Dictionary<string, string> Tokens(SolutionConfig config, V2Names names, string entity, IReadOnlyList<EntityProperty> properties, string? description)
    {
        var required = properties.Where(p => !p.IsOptional).ToList();
        var optional = properties.Where(p => p.IsOptional).ToList();
        var ordered = required.Concat(optional).ToList();
        var tokens = names.Tokens();
        var hasEnum = properties.Any(p => p.IsEnum);
        var enumUsing = hasEnum ? $"using {config.SolutionName}.Domain.Enums;\n" : string.Empty;

        tokens["EnumUsing"] = enumUsing;
        tokens["Summary"] = string.IsNullOrWhiteSpace(description) ? $"{entity} aggregate." : System.Text.RegularExpressions.Regex.Replace(description.Trim(), "[<>&\"\\r\\n]", " ");
        tokens["Constants"] = string.Concat(properties.Where(p => p.IsString).Select(p => $"    public const int {p.LengthConstant} = {p.StringLength};\n")).TrimEnd('\n');
        tokens["Properties"] = string.Join("\n\n", properties.Select(p => $"    public {p.ClrType} {p.Name} {{ get; private set; }}{(!p.IsOptional && p.IsString ? " = default!;" : string.Empty)}"));
        tokens["CreateParameters"] = string.Join(", ", required.Select(p => $"{p.ClrType} {p.ParameterName}")
            .Append("DateTime nowUtc")
            .Concat(optional.Select(p => $"{p.ClrType} {p.ParameterName} = default")));
        tokens["Assignments"] = string.Join("\n", properties.Select(p => "            " + p.Name + " = " + Assignment(p) + ","));
        tokens["Mappings"] = Mappings(entity, properties);
        tokens["DtoParameters"] = string.Join(", ", new[] { "Guid Id" }.Concat(properties.Select(p => $"{p.ClrType} {p.Name}")).Concat(new[] { "DateTime CreatedAtUtc", "DateTime? UpdatedAtUtc" }));
        tokens["DtoArguments"] = string.Join(", ", new[] { "entity.Id" }.Concat(properties.Select(p => $"entity.{p.Name}")).Concat(new[] { "entity.CreatedAtUtc", "entity.UpdatedAtUtc" }));
        tokens["SampleArguments"] = SampleArguments(ordered, entity);
        tokens["StateAssertions"] = string.Join("\n", properties.Select(p => $"        Assert.Equal({p.Sample()}, entity.{p.Name});"));
        tokens["LoadedAssertions"] = string.Join("\n", properties.Select(p => $"        Assert.Equal({p.Sample()}, loaded.{p.Name});"));
        tokens["RequiredTextTests"] = RequiredTextTests(entity, required, optional);
        tokens["UniqueTests"] = UniqueTests(entity, properties, required, optional);
        tokens["EfUsing"] = properties.Any(p => p.IsUnique) ? "using Microsoft.EntityFrameworkCore;\n" : string.Empty;
        return tokens;
    }

    private static string Assignment(EntityProperty p) => p.IsString
        ? p.IsOptional
            ? $"DomainException.OptionalText({p.ParameterName}, nameof({p.Name}), {p.LengthConstant})"
            : $"DomainException.RequireText({p.ParameterName}, nameof({p.Name}), {p.LengthConstant})"
        : p.ParameterName;

    private static string Mappings(string EntityName, IReadOnlyList<EntityProperty> properties)
    {
        var lines = new List<string>();
        foreach (var p in properties)
        {
            if (p.IsString)
                lines.Add($"        builder.Property(entity => entity.{p.Name}){(p.IsOptional ? string.Empty : ".IsRequired()")}.HasMaxLength({EntityName}.{p.LengthConstant});");
            else if (p.IsEnum)
                lines.Add($"        builder.Property(entity => entity.{p.Name}).HasConversion<string>().HasMaxLength(64);");

            if (p.IsUnique)
                lines.Add($"        builder.HasIndex(entity => entity.{p.Name}).IsUnique();");
            else if (p.IsIndexed)
                lines.Add($"        builder.HasIndex(entity => entity.{p.Name});");
        }

        return string.Join("\n", lines);
    }

    private static string SampleArguments(IReadOnlyList<EntityProperty> ordered, string entity)
    {
        var values = new List<string>();
        var nowAdded = false;
        foreach (var p in ordered)
        {
            if (p.IsOptional && !nowAdded)
            {
                values.Add("Now");
                nowAdded = true;
            }

            values.Add(p.Sample());
        }

        if (!nowAdded)
            values.Add("Now");
        return string.Join(", ", values);
    }

    private static string RequiredTextTests(string entity, IReadOnlyList<EntityProperty> required, IReadOnlyList<EntityProperty> optional)
    {
        var text = required.FirstOrDefault(p => p.IsString);
        if (text is null)
            return string.Empty;

        var ordered = required.Concat(optional).ToList();
        string Args(string replacement)
        {
            var values = new List<string>();
            var nowAdded = false;
            foreach (var p in ordered)
            {
                if (p.IsOptional && !nowAdded)
                {
                    values.Add("Now");
                    nowAdded = true;
                }

                values.Add(p == text ? replacement : p.Sample());
            }

            if (!nowAdded)
                values.Add("Now");
            return string.Join(", ", values);
        }

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("    [Theory]");
        sb.AppendLine("    [InlineData(\"\")]");
        sb.AppendLine("    [InlineData(\"   \")]");
        sb.AppendLine($"    public void Create_rejects_a_blank_{text.ParameterName}(string value) =>");
        sb.AppendLine($"        Assert.Throws<DomainException>(() => {entity}.Create({Args("value")}));");
        sb.AppendLine();
        sb.AppendLine("    [Fact]");
        sb.AppendLine($"    public void Create_rejects_a_{text.ParameterName}_longer_than_the_limit() =>");
        sb.AppendLine($"        Assert.Throws<DomainException>(() => {entity}.Create({Args($"new string('x', {entity}.{text.LengthConstant} + 1)")}));");
        return sb.ToString();
    }

    private static string UniqueTests(string entity, IReadOnlyList<EntityProperty> properties, IReadOnlyList<EntityProperty> required, IReadOnlyList<EntityProperty> optional)
    {
        var sb = new StringBuilder();
        foreach (var p in properties.Where(p => p.IsUnique))
        {
            sb.AppendLine();
            sb.AppendLine("    [Fact]");
            sb.AppendLine($"    public async Task {p.Name}_is_unique()");
            sb.AppendLine("    {");
            sb.AppendLine("        await using var write = _database.CreateContext();");
            sb.AppendLine($"        write.Add({entity}.Create({SampleArguments(required.Concat(optional).ToList(), entity)}));");
            sb.AppendLine($"        write.Add({entity}.Create({SampleArguments(required.Concat(optional).ToList(), entity)}));");
            sb.AppendLine("        await Assert.ThrowsAsync<DbUpdateException>(() => write.SaveChangesAsync());");
            sb.AppendLine("    }");
        }

        return sb.ToString();
    }
}
