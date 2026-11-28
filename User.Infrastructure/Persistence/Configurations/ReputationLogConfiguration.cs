namespace User.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using User.Domain.Entities;

public class ReputationLogConfiguration : IEntityTypeConfiguration<ReputationLog>
{
    public void Configure(EntityTypeBuilder<ReputationLog> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ReferenceId).HasMaxLength(100);
        builder.Property(x => x.Note).HasMaxLength(500);

        builder.HasIndex(x => new { x.UserId, x.TargetRole, x.PartnerUserId, x.CreatedAt });

        // One change per user, role, reference and reason. The service checks for an existing row first, but two requests at the same moment
        // both pass that check; this index makes the second one fail so a penalty is never applied twice.
        builder.HasIndex(x => new { x.UserId, x.TargetRole, x.ReferenceType, x.ReferenceId, x.Reason })
            .IsUnique()
            .HasFilter("\"ReferenceId\" IS NOT NULL")
            .HasDatabaseName("IX_ReputationLogs_Idempotency");

        builder.HasOne(l => l.User)
            .WithMany(u => u.ReputationLogs)
            .HasForeignKey(l => l.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.CreatedByUser)
            .WithMany()
            .HasForeignKey(l => l.CreatedBy)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
