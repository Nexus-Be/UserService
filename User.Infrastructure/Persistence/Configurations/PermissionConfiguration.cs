namespace User.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using User.Domain.Entities;

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PermissionCode).HasMaxLength(100).IsRequired();
        builder.Property(x => x.PermissionName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500);

        builder.HasMany(p => p.RolePermissions)
            .WithOne(rp => rp.Permission)
            .HasForeignKey(rp => rp.PermissionId);
    }
}
