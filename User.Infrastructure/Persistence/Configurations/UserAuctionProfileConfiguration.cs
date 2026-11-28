namespace User.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using User.Domain.Entities;

public class UserAuctionProfileConfiguration : IEntityTypeConfiguration<UserAuctionProfile>
{
    public void Configure(EntityTypeBuilder<UserAuctionProfile> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.RestrictionReason).HasMaxLength(1000);

        builder.HasIndex(x => x.UserId).IsUnique();

        builder.HasOne(x => x.User)
            .WithOne()
            .HasForeignKey<UserAuctionProfile>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
