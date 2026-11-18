namespace User.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using User.Domain.Entities;

public class UserAddressConfiguration : IEntityTypeConfiguration<UserAddress>
{
    public void Configure(EntityTypeBuilder<UserAddress> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RecipientName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Phone).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Label).HasMaxLength(60);
        builder.Property(x => x.ProvinceName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.DistrictName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.WardCode).HasMaxLength(50).IsRequired();
        builder.Property(x => x.WardName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.AddressLine).HasMaxLength(500).IsRequired();

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.UserId)
            .IsUnique()
            .HasDatabaseName("IX_UserAddresses_UserId_Default")
            .HasFilter("\"IsDefault\" = true AND \"IsDeleted\" = false");

        builder.HasOne(x => x.User)
            .WithMany(u => u.Addresses)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
