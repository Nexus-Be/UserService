namespace User.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using User.Domain.Entities;

public class UserPenaltyConfiguration : IEntityTypeConfiguration<UserPenalty>
{
    public void Configure(EntityTypeBuilder<UserPenalty> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();

        builder.HasOne(p => p.User)
            .WithMany(u => u.PenaltiesReceived)
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.CreatedByUser)
            .WithMany(u => u.PenaltiesIssued)
            .HasForeignKey(p => p.CreatedBy)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
