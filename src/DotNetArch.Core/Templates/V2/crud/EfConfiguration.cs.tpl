using {{App}}.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace {{App}}.Infrastructure.Persistence.Configurations;

internal sealed class {{Entity}}Configuration : EntityConfiguration<{{Entity}}>
{
    protected override void ConfigureEntity(EntityTypeBuilder<{{Entity}}> builder)
    {
        builder.ToTable("{{Plural}}");
        builder.Property(entity => entity.Name).IsRequired().HasMaxLength({{Entity}}.NameMaxLength);
    }
}
