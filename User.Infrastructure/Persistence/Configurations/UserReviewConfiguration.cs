namespace User.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using User.Domain.Entities;

public class UserReviewConfiguration : IEntityTypeConfiguration<UserReview>
{
    public void Configure(EntityTypeBuilder<UserReview> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ReferenceType).HasConversion<string>();
        builder.Property(x => x.ReferenceId).HasMaxLength(100);
        builder.Property(x => x.ReviewerRole).HasConversion<string>();
        builder.Property(x => x.Comment).HasMaxLength(2000);

        builder.HasOne(r => r.Reviewer)
            .WithMany(u => u.ReviewsGiven)
            .HasForeignKey(r => r.ReviewerId);

        builder.HasOne(r => r.Reviewee)
            .WithMany(u => u.ReviewsReceived)
            .HasForeignKey(r => r.RevieweeId);
    }
}
