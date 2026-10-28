namespace User.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using User.Domain.Entities;

public class ReputationScoreConfiguration : IEntityTypeConfiguration<ReputationScore>
{
    public void Configure(EntityTypeBuilder<ReputationScore> builder)
    {
        builder.HasKey(x => x.Id);

        builder.HasOne(r => r.User)
            .WithOne(u => u.ReputationScore)
            .HasForeignKey<ReputationScore>(r => r.UserId);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
