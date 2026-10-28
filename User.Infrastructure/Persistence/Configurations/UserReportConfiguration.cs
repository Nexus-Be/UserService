namespace User.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using User.Domain.Entities;

public class UserReportConfiguration : IEntityTypeConfiguration<UserReport>
{
    public void Configure(EntityTypeBuilder<UserReport> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Description).IsRequired();
        builder.Property(x => x.EvidenceUrls).HasColumnType("text[]");
        builder.Property(x => x.ResolutionNote).HasMaxLength(2000);

        builder.HasOne(r => r.Reporter)
            .WithMany(u => u.ReportsSubmitted)
            .HasForeignKey(r => r.ReporterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.ReportedUser)
            .WithMany(u => u.ReportsReceived)
            .HasForeignKey(r => r.ReportedUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.ResolvedByUser)
            .WithMany(u => u.ReportsResolved)
            .HasForeignKey(r => r.ResolvedBy)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
