namespace User.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using User.Domain.Entities;

public class ReputationLogConfiguration : IEntityTypeConfiguration<ReputationLog>
{
    public void Configure(EntityTypeBuilder<ReputationLog> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Reason).HasConversion<string>();
        builder.Property(x => x.ReferenceType).HasConversion<string>();
        builder.Property(x => x.ReferenceId).HasMaxLength(100);

        builder.HasOne(l => l.User)
            .WithMany(u => u.ReputationLogs)
            .HasForeignKey(l => l.UserId);
    }
}
