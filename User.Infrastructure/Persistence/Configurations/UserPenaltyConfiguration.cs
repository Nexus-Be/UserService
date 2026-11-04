namespace User.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using User.Domain.Entities;
using User.Domain.Enums;

public class UserPenaltyConfiguration : IEntityTypeConfiguration<UserPenalty>
{
    public void Configure(EntityTypeBuilder<UserPenalty> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        builder.Property(x => x.TargetRole).HasDefaultValue(TargetRole.Account).HasSentinel(TargetRole.Account);
        builder.Property(x => x.PolicyClauseCode).HasMaxLength(100);
        builder.Property(x => x.RevokeReason).HasMaxLength(1000);
        builder.Property(x => x.EvidenceUrls).HasColumnType("text[]");

        builder.HasOne(p => p.User)
            .WithMany(u => u.PenaltiesReceived)
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.CreatedByUser)
            .WithMany(u => u.PenaltiesIssued)
            .HasForeignKey(p => p.CreatedBy)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(p => p.RevokedByUser)
            .WithMany(u => u.PenaltiesRevoked)
            .HasForeignKey(p => p.RevokedBy)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(p => p.Report)
            .WithMany()
            .HasForeignKey(p => p.ReportId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(p => new { p.UserId, p.TargetRole, p.PenaltyApplied, p.ExpiresAt })
            .HasDatabaseName("IX_UserPenalties_Active_Evaluation")
            .HasFilter("\"Status\" = 0");

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
