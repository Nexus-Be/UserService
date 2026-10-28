namespace User.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using User.Domain.Entities;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Username).HasMaxLength(100);
        builder.HasIndex(x => x.Username).IsUnique().HasFilter("\"Username\" IS NOT NULL");
        builder.Property(x => x.Email).HasMaxLength(255).IsRequired();
        builder.HasIndex(x => x.Email).IsUnique();
        builder.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
        builder.Property(x => x.FirstName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.LastName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.PhoneNumber).HasMaxLength(50);
        builder.Property(x => x.IdentifyNumber).HasMaxLength(50);
        builder.Property(x => x.AvatarUrl).HasMaxLength(500);
        builder.Property(x => x.Address).HasMaxLength(1000);
        builder.Property(x => x.BanReason).HasMaxLength(1000);

        builder.HasOne(u => u.BannedByUser)
            .WithMany()
            .HasForeignKey(u => u.BannedBy)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(u => u.RefreshTokens)
            .WithOne(t => t.User)
            .HasForeignKey(t => t.UserId);

        builder.HasOne(u => u.SellerProfile)
            .WithOne(s => s.User)
            .HasForeignKey<SellerProfile>(s => s.UserId);

        builder.HasOne(u => u.ReputationScore)
            .WithOne(r => r.User)
            .HasForeignKey<ReputationScore>(r => r.UserId);

        builder.HasMany(u => u.ReputationLogs)
            .WithOne(l => l.User)
            .HasForeignKey(l => l.UserId);

        builder.HasMany(u => u.ReviewsGiven)
            .WithOne(r => r.Reviewer)
            .HasForeignKey(r => r.ReviewerId);

        builder.HasMany(u => u.ReviewsReceived)
            .WithOne(r => r.Reviewee)
            .HasForeignKey(r => r.RevieweeId);

        builder.HasMany(u => u.ReportsSubmitted)
            .WithOne(r => r.Reporter)
            .HasForeignKey(r => r.ReporterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(u => u.ReportsReceived)
            .WithOne(r => r.ReportedUser)
            .HasForeignKey(r => r.ReportedUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(u => u.ReportsResolved)
            .WithOne(r => r.ResolvedByUser)
            .HasForeignKey(r => r.ResolvedBy)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(u => u.PenaltiesReceived)
            .WithOne(p => p.User)
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.PenaltiesIssued)
            .WithOne(p => p.CreatedByUser)
            .HasForeignKey(p => p.CreatedBy)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
