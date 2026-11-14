namespace User.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using User.Domain.Entities;

public class AuctionViolationConfiguration : IEntityTypeConfiguration<AuctionViolation>
{
    public void Configure(EntityTypeBuilder<AuctionViolation> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.DepositForfeitedAmount).HasPrecision(18, 2);
        builder.Property(x => x.Reason).HasMaxLength(1000);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.UserId, x.CreatedAt })
            .HasDatabaseName("IX_AuctionViolations_UserId_CreatedAt");

        builder.HasIndex(x => new { x.UserId, x.AuctionId, x.ViolationType })
            .IsUnique()
            .HasDatabaseName("IX_AuctionViolations_System_Idempotency")
            .HasFilter("\"Source\" = 0 AND \"AuctionId\" IS NOT NULL AND \"ViolationType\" IS NOT NULL");

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
