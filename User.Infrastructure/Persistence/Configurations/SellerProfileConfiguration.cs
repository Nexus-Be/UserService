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
        builder.Property(x => x.AvatarUrl).HasMaxLength(500);
        builder.Property(x => x.BusinessEmail).HasMaxLength(255);
        builder.Property(x => x.IdCardFrontUrl).HasMaxLength(500);
        builder.Property(x => x.IdCardBackUrl).HasMaxLength(500);
        builder.Property(x => x.BusinessLicenseUrl).HasMaxLength(500);
        builder.Property(x => x.PickupAddress).HasMaxLength(500).IsRequired();
        builder.Property(x => x.WardCode).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Hotline).HasMaxLength(50).IsRequired();
        builder.Property(x => x.RejectionReason).HasMaxLength(1000);
        builder.Property(x => x.Status).HasConversion<string>();

        builder.HasOne(s => s.User)
            .WithOne(u => u.SellerProfile)
            .HasForeignKey<SellerProfile>(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.ReviewedByUser)
            .WithMany()
            .HasForeignKey(s => s.ReviewedBy)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
