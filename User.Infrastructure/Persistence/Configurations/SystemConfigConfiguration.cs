namespace User.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using User.Domain.Entities;

public class SystemConfigConfiguration : IEntityTypeConfiguration<SystemConfig>
{
    public void Configure(EntityTypeBuilder<SystemConfig> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ConfigKey).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.ConfigKey).IsUnique();
        builder.Property(x => x.ConfigValue).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);
    }
}
