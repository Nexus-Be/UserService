namespace User.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using User.Domain.Entities;

public class SellerProfileConfiguration : IEntityTypeConfiguration<SellerProfile>
{
    public void Configure(EntityTypeBuilder<SellerProfile> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ShopName).HasMaxLength(255).IsRequired();
        builder.HasIndex(x => x.ShopName).IsUnique();
        builder.Property(x => x.PickupAddress).HasMaxLength(500).IsRequired();
        builder.Property(x => x.WardCode).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Hotline).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>();
        builder.Property(x => x.RejectionReason).HasMaxLength(1000);

        builder.HasOne(s => s.User)
            .WithOne(u => u.SellerProfile)
            .HasForeignKey<SellerProfile>(s => s.UserId);
    }
}
